using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MES_EDWS.Models;
using MES_EDWS.Services;

namespace MES_EDWS.Controllers
{
    /// <remarks>
    /// Deliberately not an [ApiController]: its automatic 400 would reject a payload with a missing
    /// requestId (implicitly required as a non-nullable string) or malformed JSON before the action
    /// runs, and the fields that were provided must still be saved.
    /// </remarks>
    [Route(ApiRoute)]
    public class MedicallyFrailController : ControllerBase
    {
        public const string ApiRoute = "api/mes/medically-frail";

        private const string LookupFailedMessage =
            "The system could not process your request at this time. Please try after some time. If the issue persists, please contact helpdesk.";

        private readonly ILogger<MedicallyFrailController> _logger;
        private readonly IMedicalFrailtyService _medicalFrailtyService;
        private readonly JsonSerializerOptions _jsonSerializerOptions;

        public MedicallyFrailController(
            ILogger<MedicallyFrailController> logger,
            IMedicalFrailtyService medicalFrailtyService,
            IOptions<JsonOptions> jsonOptions)
        {
            _logger = logger;
            _medicalFrailtyService = medicalFrailtyService;
            _jsonSerializerOptions = jsonOptions.Value.JsonSerializerOptions;
        }

        [HttpPost]
        public async Task<IActionResult> GetMedicallyFrailStatus([FromBody] MedicallyFrailRequest? request)
        {
            var requestTime   = DateTime.Now;
            var transactionId = Guid.NewGuid().ToString();
            var requestJson   = await Request.ReadStorableJsonAsync();

            var (statusCode, body) = await ProcessAsync(request, transactionId);

            // Persist to MWRP_CE_MED_FRAIL_API_* (non-blocking — failures are logged, not thrown).
            await _medicalFrailtyService.SaveApiAuditAsync(new ApiAudit
            {
                RequestId     = string.IsNullOrWhiteSpace(request?.RequestId)
                                    ? transactionId
                                    : request.RequestId,
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

        private async Task<(int StatusCode, object Body)> ProcessAsync(MedicallyFrailRequest? request, string transactionId)
        {
            // Body was empty or not valid JSON for this DTO, so there is nothing to save.
            if (request == null)
            {
                var invalidPaths = ModelState
                    .Where(entry => entry.Value?.Errors.Count > 0 && !string.IsNullOrEmpty(entry.Key))
                    .Select(entry => entry.Key)
                    .ToList();

                _logger.LogWarning(
                    "Malformed medically frail payload. TransactionId: {TransactionId}, Errors: {Errors}",
                    transactionId,
                    string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));

                return (StatusCodes.Status400BadRequest, BadRequestBody(invalidPaths));
            }

            _logger.LogInformation(
                "Received medically frail request. RequestId: {RequestId}, MmisEnrolleeId: {MmisEnrolleeId}, TransactionId: {TransactionId}",
                request.RequestId,
                request.MmisEnrolleeId,
                transactionId);

            var missingFields = GetMissingRequiredFields(request);
            if (missingFields.Count > 0)
            {
                _logger.LogWarning(
                    "Medically frail payload missing required fields: {MissingFields}. TransactionId: {TransactionId}",
                    string.Join(", ", missingFields), transactionId);

                var badRequest = BadRequestBody(missingFields);

                // Save what was provided (non-blocking — failures are logged, not thrown).
                await _medicalFrailtyService.SaveRequestAsync(
                    request.RequestId, request.MmisEnrolleeId, request.Ssn);
                await _medicalFrailtyService.SaveResponseAsync(
                    request.RequestId, null, null, null,
                    code:    badRequest.Code.ToString(),
                    message: badRequest.Message);

                return (StatusCodes.Status400BadRequest, badRequest);
            }

            // Persist the incoming request to Teradata (non-blocking — failures are logged, not thrown).
            await _medicalFrailtyService.SaveRequestAsync(
                request.RequestId, request.MmisEnrolleeId, request.Ssn);

            // Lookup in HR1_MEDICALLY_FRAIL_MEMBERS by MMIS_ENROLLEE_ID, then SSN.
            MedicalFrailtyRecord? record;
            try
            {
                record = await _medicalFrailtyService.GetByMmisEnrolleeIdOrSsnAsync(
                    request.RequestId, request.MmisEnrolleeId, request.Ssn);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Teradata lookup failed for RequestId: {RequestId}", request.RequestId);

                await _medicalFrailtyService.SaveResponseAsync(
                    request.RequestId, "N", null, null,
                    code:    "5000",
                    message: LookupFailedMessage);

                return (StatusCodes.Status500InternalServerError, new
                {
                    Code    = 5000,
                    Message = LookupFailedMessage
                });
            }

            if (record == null)
            {
                _logger.LogWarning(
                    "No medical frailty record found for RequestId: {RequestId}, MmisEnrolleeId: {MmisEnrolleeId}",
                    request.RequestId, request.MmisEnrolleeId);

                await _medicalFrailtyService.SaveResponseAsync(
                    request.RequestId, "N", null, null,
                    code:    "200",
                    message: "Success");

                return (StatusCodes.Status200OK, new
                {
                    request               = request.RequestId,
                    medicallyFrail        = "N",
                    circumstanceStartDate = "",
                    circumstanceEndDate   = "",
                    Code                  = "200",
                    Message               = "Success"
                });
            }

            var response = new MedicallyFrailResponse
            {
                RequestId             = request.RequestId,
                MedicallyFrail        = record.MedicallyFrail ? "Y" : "N",
                CircumstanceStartDate = record.CircumstanceStartDate,
                CircumstanceEndDate   = record.CircumstanceEndDate,
                Code                  = "200",
                Message               = "Success"
            };

            // Persist the response to Teradata.
            await _medicalFrailtyService.SaveResponseAsync(
                response.RequestId,
                response.MedicallyFrail,
                response.CircumstanceStartDate,
                response.CircumstanceEndDate,
                code:    response.Code,
                message: response.Message);

            return (StatusCodes.Status200OK, response);
        }

        private static List<string> GetMissingRequiredFields(MedicallyFrailRequest request)
        {
            var missing = new List<string>();

            if (string.IsNullOrWhiteSpace(request.RequestId))
                missing.Add("requestId");

            // At least one identifier must be present.
            if (string.IsNullOrWhiteSpace(request.MmisEnrolleeId) &&
                string.IsNullOrWhiteSpace(request.Ssn))
                missing.Add("mmisEnrolleeId or ssn");

            return missing;
        }

        private static BadRequestResponse BadRequestBody(IReadOnlyCollection<string> fields) => new(
            Code:    4000,
            Message: fields.Count > 0
                ? $"Malformed JSON or missing required field: {string.Join(", ", fields)}"
                : "Malformed JSON or missing required field");

        private sealed record BadRequestResponse(int Code, string Message);
    }
}
