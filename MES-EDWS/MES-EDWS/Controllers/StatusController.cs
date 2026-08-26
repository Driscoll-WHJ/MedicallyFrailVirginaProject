using Microsoft.AspNetCore.Mvc;

namespace MES_EDWS.Controllers
{
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("status")]
    [ApiController]
    public class StatusController : ControllerBase
    {
        private readonly ILogger<StatusController> _logger;

        public StatusController(ILogger<StatusController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public IActionResult GetHealthStatus()
        {
            _logger.LogInformation("Status check endpoint called");

            var healthStatus = new
            {
                serviceId = "HR1 APIs",
                status = "All systems operational",
                version = "1.0.0",
                state = "normal",
                lastStatusUpdateTime = DateTime.UtcNow.ToString("ddd, dd MMM yyyy HH:mm:ss 'UTC'")
            };

            return Ok(healthStatus);
        }
    }
}

