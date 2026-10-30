using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;
using MES_EDWS.Models;
using MES_EDWS.Services;

namespace MES_EDWS.Controllers
{
    /// <summary>
    /// Receives community engagement (CE) verification results from the CEP system via MES-ISS.
    /// ICD Reference: CEP-ICD-003 CEP to EDWS v1.0
    /// </summary>
    /// <remarks>
    /// Deliberately not an [ApiController]: its automatic 400 would reject a payload with missing
    /// [Required] fields before the action runs, and the fields that were provided must still be saved.
    /// </remarks>
    [Route(ApiRoute)]
    public class ClientInfoController : ControllerBase
    {
        public const string ApiRoute = "api/nvh/verification-request";

        private const string SaveFailedMessage =
            "The system could not process your request at this time. Please try after some time. If the issue persists, please contact helpdesk.";

        private readonly ILogger<ClientInfoController> _logger;
        private readonly IClientInfoService _clientInfoService;
        private readonly JsonSerializerOptions _jsonSerializerOptions;

        public ClientInfoController(
            ILogger<ClientInfoController> logger,
            IClientInfoService clientInfoService,
            IOptions<JsonOptions> jsonOptions)
        {
            _logger = logger;
            _clientInfoService = clientInfoService;
            _jsonSerializerOptions = jsonOptions.Value.JsonSerializerOptions;
        }

        /// <summary>
        /// Accepts a CE verification result payload from CEP for one or more individuals
        /// and returns a synchronous acknowledgement.
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(CepDWAckResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(CepDWResultDTO), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(CepDWResultDTO), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ReceiveCeVerificationResults([FromBody] CepDWRequestDTO? request)
        {
            var requestTime   = DateTime.Now;
            var transactionId = Guid.NewGuid().ToString();
            var requestJson   = await Request.ReadStorableJsonAsync();

            var (statusCode, body) = await ProcessAsync(request, requestJson, transactionId);

            // Persist to MWRP_CE_RESPONSE for every response (non-blocking — failures are logged, not thrown).
            await _clientInfoService.SaveResponseAsync(request, transactionId, statusCode, body);

            // Persist to MWRP_CE_ERROR_* / MWRP_CE_INVALID_FIELD (non-blocking — failures are logged, not thrown).
            if (body is CepDWResultDTO errorResult)
                await _clientInfoService.SaveErrorResultAsync(errorResult);

            // Persist to MWRP_CE_API_* (non-blocking — failures are logged, not thrown).
            await _clientInfoService.SaveApiAuditAsync(new ApiAudit
            {
                RequestId     = string.IsNullOrWhiteSpace(request?.RequestSequenceNumber)
                                    ? transactionId
                                    : request.RequestSequenceNumber,
                TransactionId = transactionId,
                ApiName       = "/" + ApiRoute,
                HttpMethod    = HttpMethods.Post,
                StatusCode    = statusCode,
                RequestTime   = requestTime,
                ResponseTime  = DateTime.Now,
                RequestJson   = requestJson,
                ResponseJson  = JsonSerializer.Serialize(body, _jsonSerializerOptions)
            });

            return StatusCode(statusCode, body);
        }

        private async Task<(int StatusCode, object Body)> ProcessAsync(
            CepDWRequestDTO? request, string? requestJson, string transactionId)
        {
            // Body was empty or not valid JSON for this DTO, so there is nothing to save.
            if (request == null || requestJson == null)
            {
                var invalidFields = ModelState
                    .Where(entry => !string.IsNullOrEmpty(entry.Key))
                    .SelectMany(entry => (entry.Value?.Errors ?? Enumerable.Empty<ModelError>())
                        .Select(error => InvalidField(entry.Key, FormatRule, error.ErrorMessage)))
                    .ToList();

                if (invalidFields.Count == 0)
                    invalidFields.Add(InvalidField("$", FormatRule, "Request body is empty or is not valid JSON."));

                _logger.LogWarning(
                    "Malformed CE verification payload. TransactionId: {TransactionId}, Errors: {Errors}",
                    transactionId,
                    string.Join("; ", invalidFields.Select(f => f.Message)));

                return (StatusCodes.Status400BadRequest, ValidationError(transactionId, invalidFields));
            }

            _logger.LogInformation(
                "Received CE verification results. TransactionId: {TransactionId}, CaseNumber: {CaseNumber}, " +
                "RequestSequenceNumber: {RequestSequenceNumber}, State: {State}, " +
                "RequestSource: {RequestSource}, ClientIdentificationNumber: {ClientIdentificationNumber}",
                transactionId,
                request.CaseNumber,
                request.RequestSequenceNumber,
                request.State,
                request.RequestSource,
                request.CeVerified is { Count: > 0 } ? request.CeVerified[0]?.ClientIdentificationNumber : null);

            var missingFields = GetInvalidRequiredFields(request, requestJson);
            if (missingFields.Count > 0)
            {
                _logger.LogWarning(
                    "CE verification payload missing required fields: {MissingFields}. TransactionId: {TransactionId}",
                    string.Join(", ", missingFields.Select(f => f.Field)), transactionId);

                try
                {
                    await _clientInfoService.SavePartialCeVerificationResultsAsync(request);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to save partial CE verification payload. TransactionId: {TransactionId}", transactionId);
                }

                return (StatusCodes.Status400BadRequest, ValidationError(transactionId, missingFields));
            }

            try
            {
                // Persists the request (MWRP_CE_REQUEST and below) inside one transaction.
                var response = await _clientInfoService.SaveCeVerificationResultsAsync(request);

                _logger.LogInformation(
                    "CE verification acknowledged. AckRequestId: {AckRequestId}, CaseNumber: {CaseNumber}",
                    response.Acknowledgement.RequestId,
                    request.CaseNumber);

                return (StatusCodes.Status200OK, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to save CE verification results. TransactionId: {TransactionId}", transactionId);

                return (StatusCodes.Status500InternalServerError, SystemError(transactionId));
            }
        }

        /// <summary>
        /// Every [Required] element (per CEP-003 ICD v1.7.4) absent from the body (rule "Required");
        /// a required element sent as null or "" is accepted, except requestSequenceNumber, which keys
        /// every MWRP_CE_* data table and so must have a value. Plus a requestTimestamp that has a value
        /// but is not an ISO 8601 date-time (rule "Format").
        /// </summary>
        private static List<InvalidFieldDTO> GetInvalidRequiredFields(CepDWRequestDTO request, string requestJson)
        {
            using var document = JsonDocument.Parse(requestJson);
            var missing = RequiredFieldValidator.GetMissingFields(document.RootElement, typeof(CepDWRequestDTO));
            var invalid = missing.Select(MissingField).ToList();

            if (string.IsNullOrWhiteSpace(request.RequestSequenceNumber) && !missing.Contains(RequestSequenceNumberField))
                invalid.Insert(0, InvalidField(RequestSequenceNumberField, RequiredRule,
                    $"Malformed JSON or missing required field: {RequestSequenceNumberField} must have a value."));

            if (!string.IsNullOrWhiteSpace(request.Timestamp) && !request.TryGetRequestTimestamp(out _))
                invalid.Insert(0, InvalidField("requestTimestamp", FormatRule,
                    "Malformed JSON or missing required field: requestTimestamp must be an ISO 8601 date-time (e.g. 2026-10-06T14:30:00-04:00)."));

            return invalid;
        }

        // ── CepDWResultDTO (CEP-003 ICD v1.7.4 "Error Response") ──────────────────

        private const int ValidationErrorCode = 400;
        private const int SystemErrorCode     = 500;
        private const string RequiredRule     = "Required";
        private const string FormatRule       = "Format";
        private const string RequestSequenceNumberField = "requestSequenceNumber";

        private static InvalidFieldDTO MissingField(string field) =>
            InvalidField(field, RequiredRule, $"Malformed JSON or missing required field: {field}");

        private static InvalidFieldDTO InvalidField(string field, string rule, string message) => new()
        {
            Field   = field,
            Code    = rule,
            Message = message
        };

        /// <summary>400 — the caller must correct the listed fields and resubmit, so notify = true.</summary>
        private static CepDWResultDTO ValidationError(string correlationId, List<InvalidFieldDTO> invalidFields) =>
            Result(correlationId, ValidationErrorCode, notify: true, invalidFields);

        /// <summary>500 — an EDWS-side failure with nothing for the caller to correct, so notify = false.</summary>
        private static CepDWResultDTO SystemError(string correlationId) =>
            Result(correlationId, SystemErrorCode, notify: false, new List<InvalidFieldDTO>
            {
                InvalidField("$", "System", SaveFailedMessage)
            });

        private static CepDWResultDTO Result(
            string correlationId, int code, bool notify, List<InvalidFieldDTO> invalidFields) => new()
        {
            Timestamp     = DateTime.UtcNow,
            CorrelationId = correlationId,
            Message       = new ErrorMessageDTO
            {
                Code   = code,
                Type   = "ERROR",
                Notify = notify
            },
            Error = new ErrorDetailDTO
            {
                InvalidFields = invalidFields
            }
        };
    }
}
