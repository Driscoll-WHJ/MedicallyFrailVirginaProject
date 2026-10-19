using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
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
        public async Task<IActionResult> ReceiveCeVerificationResults([FromBody] CepDWRequestDTO? request)
        {
            var requestTime   = DateTime.Now;
            var transactionId = Guid.NewGuid().ToString();
            var requestJson   = await Request.ReadStorableJsonAsync();

            var (statusCode, body) = await ProcessAsync(request, transactionId);

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
                var invalidPaths = ModelState
                    .Where(entry => entry.Value?.Errors.Count > 0 && !string.IsNullOrEmpty(entry.Key))
                    .Select(entry => entry.Key)
                    .ToList();

                _logger.LogWarning(
                    "Malformed CE verification payload. TransactionId: {TransactionId}, Errors: {Errors}",
                    transactionId,
                    string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));

                return (StatusCodes.Status400BadRequest, BadRequestBody(invalidPaths));
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

            var missingFields = GetMissingRequiredFields(request);
            if (missingFields.Count > 0)
            {
                _logger.LogWarning(
                    "CE verification payload missing required fields: {MissingFields}. TransactionId: {TransactionId}",
                    string.Join(", ", missingFields), transactionId);

                try
                {
                    await _clientInfoService.SavePartialCeVerificationResultsAsync(request);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to save partial CE verification payload. TransactionId: {TransactionId}", transactionId);
                }

                return (StatusCodes.Status400BadRequest, BadRequestBody(missingFields));
            }

            try
            {
                // Persists both the request (MWRP_CE_REQUEST) and this same response (MWRP_CE_RESPONSE)
                // to the MWRP_CE_* Teradata tables inside one transaction.
                var response = await _clientInfoService.SaveCeVerificationResultsAsync(request);

                _logger.LogInformation(
                    "CE verification acknowledged. NvhRequestId: {NvhRequestId}, CaseNumber: {CaseNumber}",
                    response.Acknowledgement.NvhRequestId,
                    request.CaseNumber);

                return (StatusCodes.Status200OK, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to save CE verification results. TransactionId: {TransactionId}", transactionId);

                return (StatusCodes.Status500InternalServerError, new
                {
                    code    = StatusCodes.Status500InternalServerError,
                    message = SaveFailedMessage
                });
            }
        }

        /// <summary>
        /// JSON paths of required fields that are absent, blank, or (for requestTimestamp) unparseable.
        /// </summary>
        private static List<string> GetMissingRequiredFields(CepDWRequestDTO request)
        {
            var missing = new List<string>();

            if (!request.TryGetRequestTimestamp(out _))
                missing.Add("requestTimestamp");
            if (string.IsNullOrWhiteSpace(request.RequestSequenceNumber))
                missing.Add("requestSequenceNumber");
            if (string.IsNullOrWhiteSpace(request.State))
                missing.Add("stateId");
            if (string.IsNullOrWhiteSpace(request.RequestSource))
                missing.Add("requestSource");

            if (request.CeVerified is not { Count: > 0 })
            {
                missing.Add("ceVerified");
            }
            else
            {
                for (var i = 0; i < request.CeVerified.Count; i++)
                {
                    if (string.IsNullOrWhiteSpace(request.CeVerified[i]?.ClientIdentificationNumber))
                        missing.Add($"ceVerified[{i}].clientId");
                }
            }

            return missing;
        }

        private static object BadRequestBody(IReadOnlyCollection<string> fields) => new
        {
            code    = StatusCodes.Status400BadRequest,
            message = fields.Count > 0
                ? $"Malformed JSON or missing required field: {string.Join(", ", fields)}"
                : "Malformed JSON or missing required field"
        };
    }
}
