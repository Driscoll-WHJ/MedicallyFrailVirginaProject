using Microsoft.AspNetCore.Mvc;

namespace MES_EDWS.Controllers
{
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("status")]
    [ApiController]
    public class StatusController : ControllerBase
    {
        private readonly ILogger<StatusController> _logger;
        private readonly IWebHostEnvironment _environment;

        public StatusController(ILogger<StatusController> logger, IWebHostEnvironment environment)
        {
            _logger = logger;
            _environment = environment;
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
                // ASPNETCORE_ENVIRONMENT (Development / SIT / UAT / Production) — lets a deployment
                // be checked against the environment it was published to.
                environment = _environment.EnvironmentName,
                lastStatusUpdateTime = DateTime.UtcNow.ToString("ddd, dd MMM yyyy HH:mm:ss 'UTC'")
            };

            return Ok(healthStatus);
        }
    }
}

