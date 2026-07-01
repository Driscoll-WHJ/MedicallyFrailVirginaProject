using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES_EDWS.Data;
using MES_EDWS.Models;
using MES_EDWS.Services;

namespace MES_EDWS.Controllers
{
    [ApiController]
    [Route("api/mes/medically-frail")]
    //[Authorize] // TODO: Re-enable once client certificate is received
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

            // Query Teradata: first by mmisEnrolleeId, fall back to SSN
            MedicalFrailtyRecord? record;
            try
            {
                record = await _medicalFrailtyService.GetByMmisEnrolleeIdOrSsnAsync(
                    request.RequestId, request.MmisEnrolleeId, request.Ssn);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Teradata lookup failed for RequestId: {RequestId}", request.RequestId);
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    errorCode = 5000,
                    message = "The system could not process your request at this time. Please try after some time. If the issue persists, please contact helpdesk."
                });
            }

            if (record == null)
            {
                _logger.LogWarning(
                    "No medical frailty record found for RequestId: {RequestId}, MmisEnrolleeId: {MmisEnrolleeId}",
                    request.RequestId,
                    request.MmisEnrolleeId);

                return NotFound(new
                {
                    errorCode = 8000,
                    message = "No medical frailty record found for the provided identifiers."
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

            // Persist audit log entry
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
