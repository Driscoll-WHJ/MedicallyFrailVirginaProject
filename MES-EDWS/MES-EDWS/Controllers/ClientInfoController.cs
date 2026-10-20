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

            var (statusCode, body) = await ProcessAsync(request, transactionId);

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

        private async Task<(int StatusCode, object Body)> ProcessAsync(CepDWRequestDTO? request, string transactionId)
        {
            // Body was empty or not valid JSON for this DTO, so there is nothing to save.
            if (request == null)
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

            var missingFields = GetInvalidRequiredFields(request);
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
                // Persists both the request (MWRP_CE_REQUEST) and this same response (MWRP_CE_RESPONSE)
                // to the MWRP_CE_* Teradata tables inside one transaction.
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
        /// Required fields that are absent or blank (rule "Required"), plus a requestTimestamp that
        /// is present but not an ISO 8601 date-time (rule "Format").
        /// </summary>
        private static List<InvalidFieldDTO> GetInvalidRequiredFields(CepDWRequestDTO request)
        {
            var invalid = new List<InvalidFieldDTO>();

            if (string.IsNullOrWhiteSpace(request.Timestamp))
                invalid.Add(MissingField("requestTimestamp"));
            else if (!request.TryGetRequestTimestamp(out _))
                invalid.Add(InvalidField("requestTimestamp", FormatRule,
                    "Malformed JSON or missing required field: requestTimestamp must be an ISO 8601 date-time (e.g. 2026-10-06T14:30:00-04:00)."));

            if (string.IsNullOrWhiteSpace(request.RequestSequenceNumber))
                invalid.Add(MissingField("requestSequenceNumber"));
            if (string.IsNullOrWhiteSpace(request.State))
                invalid.Add(MissingField("stateId"));
            if (string.IsNullOrWhiteSpace(request.RequestSource))
                invalid.Add(MissingField("requestSource"));

            if (request.CeVerified is not { Count: > 0 })
            {
                invalid.Add(MissingField("ceVerified"));
            }
            else
            {
                for (var i = 0; i < request.CeVerified.Count; i++)
                {
                    if (string.IsNullOrWhiteSpace(request.CeVerified[i]?.ClientIdentificationNumber))
                        invalid.Add(MissingField($"ceVerified[{i}].clientId"));
                }
            }

            return invalid;
        }

        // ── CepDWResultDTO (CEP-003 ICD v1.7.3 "Error Response") ──────────────────

        private const int ValidationErrorCode = 101;
        private const int SystemErrorCode     = 500;
        private const string RequiredRule     = "Required";
        private const string FormatRule       = "Format";
        private const string ResultSource     = CepDWResponseDefaults.Source;
        private const string ResultUpdateSw   = CepDWResponseDefaults.UpdateSw;

        private static InvalidFieldDTO MissingField(string field) =>
            InvalidField(field, RequiredRule, $"Malformed JSON or missing required field: {field}");

        private static InvalidFieldDTO InvalidField(string field, string rule, string message) => new()
        {
            Field    = field,
            Code     = rule,
            Message  = message,
            Source   = ResultSource,
            UpdateSw = ResultUpdateSw
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
                Code     = code,
                Type     = "ERROR",
                Notify   = notify,
                Source   = ResultSource,
                UpdateSw = ResultUpdateSw
            },
            Error = new ErrorDetailDTO
            {
                InvalidFields = invalidFields,
                Source        = ResultSource,
                UpdateSw      = ResultUpdateSw
            },
            Source   = ResultSource,
            UpdateSw = ResultUpdateSw
        };
    }
}
