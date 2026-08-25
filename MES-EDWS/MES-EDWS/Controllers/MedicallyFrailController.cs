using Microsoft.AspNetCore.Authentication.Certificate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES_EDWS.Data;
using MES_EDWS.Models;
using MES_EDWS.Services;

namespace MES_EDWS.Controllers
{
    [ApiController]
    [Route("api/mes/medically-frail")]
    //[Authorize(AuthenticationSchemes = CertificateAuthenticationDefaults.AuthenticationScheme)]
    public class MedicallyFrailController : ControllerBase
    {
        private readonly ILogger<MedicallyFrailController> _logger;
        private readonly IMedicalFrailtyService _medicalFrailtyService;
        private readonly AuditDbContext _auditDb;

        public MedicallyFrailController(
            ILogger<MedicallyFrailController> logger,
            IMedicalFrailtyService medicalFrailtyService,
            AuditDbContext auditDb)
        {
            _logger = logger;
            _medicalFrailtyService = medicalFrailtyService;
            _auditDb = auditDb;
        }

        [HttpPost]
        public async Task<IActionResult> GetMedicallyFrailStatus([FromBody] MedicallyFrailRequest request)
        {
            _logger.LogInformation(
                "Received medically frail request. RequestId: {RequestId}, MmisEnrolleeId: {MmisEnrolleeId}",
                request.RequestId,
                request.MmisEnrolleeId);

            // At least one identifier must be present.
            if (string.IsNullOrWhiteSpace(request.MmisEnrolleeId) &&
                string.IsNullOrWhiteSpace(request.Ssn))
            {
                return BadRequest(new
                {
                    Code    = 4000,
                    Message = "Either mmisEnrolleeId or ssn must be provided."
                });
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
                    errorCode: "5000",
                    errorMessage: "Internal error during member lookup.");

                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    Code    = 5000,
                    Message = "The system could not process your request at this time. Please try after some time. If the issue persists, please contact helpdesk."
                });
            }

            if (record == null)
            {
                _logger.LogWarning(
                    "No medical frailty record found for RequestId: {RequestId}, MmisEnrolleeId: {MmisEnrolleeId}",
                    request.RequestId, request.MmisEnrolleeId);

                await _medicalFrailtyService.SaveResponseAsync(
                    request.RequestId, "N", null, null,
                    errorCode: "200",
                    errorMessage: "Success");

                return Ok(new
                {
                    request               = request.RequestId,
                    medicallyFrail        = "N",
                    circumstanceStartDate = "",
                    circumstanceEndDate   = "",
                    Code                  = "200",
                    Message               = "Success"
                });
            }

            var medicallyFrailFlag = record.MedicallyFrail ? "Y" : "N";

            // Persist the response to Teradata.
            await _medicalFrailtyService.SaveResponseAsync(
                request.RequestId,
                medicallyFrailFlag,
                record.CircumstanceStartDate,
                record.CircumstanceEndDate,
                errorCode:    null,
                errorMessage: null);

            var response = new MedicallyFrailResponse
            {
                RequestId             = request.RequestId,
                MedicallyFrail        = medicallyFrailFlag,
                CircumstanceStartDate = record.CircumstanceStartDate,
                CircumstanceEndDate   = record.CircumstanceEndDate,
                Code                  = "200",
                Message               = "Success"
            };

            // Also persist to the SQLite audit log (used by the web UI audit viewer).
            var auditEntry = new MedicalFrailtyAuditLog
            {
                RequestId             = request.RequestId,
                DateRequested         = DateTime.UtcNow,
                MmisEnrolleeId        = request.MmisEnrolleeId,
                Ssn                   = request.Ssn,
                MedicallyFrail        = record.MedicallyFrail,
                CircumstanceStartDate = record.CircumstanceStartDate
            };

            _auditDb.MedicalFrailtyAuditLogs.Add(auditEntry);
            await _auditDb.SaveChangesAsync();

            _logger.LogInformation("Audit log saved for RequestId: {RequestId}", request.RequestId);

            return Ok(response);
        }
    }
}
