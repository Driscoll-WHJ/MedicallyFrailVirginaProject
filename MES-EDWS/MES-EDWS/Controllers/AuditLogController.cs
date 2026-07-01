using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES_EDWS.Data;

namespace MES_EDWS.Controllers
{
    [Authorize(AuthenticationSchemes = "CookieAuth")]
    public class AuditLogController : Controller
    {
        private readonly AuditDbContext _auditDb;

        public AuditLogController(AuditDbContext auditDb)
        {
            _auditDb = auditDb;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Data()
        {
            var logs = _auditDb.MedicalFrailtyAuditLogs
                .OrderByDescending(l => l.DateRequested)
                .Select(l => new
                {
                    l.Id,
                    l.RequestId,
                    DateRequested = l.DateRequested.ToString("yyyy-MM-dd HH:mm:ss"),
                    l.MmisEnrolleeId,
                    Ssn = l.Ssn ?? "",
                    MedicallyFrail = l.MedicallyFrail ? "Yes" : "No",
                    CircumstanceStartDate = l.CircumstanceStartDate ?? ""
                })
                .ToList();

            return Json(new { data = logs });
        }
    }
}
