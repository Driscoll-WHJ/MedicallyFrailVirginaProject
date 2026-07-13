using Microsoft.AspNetCore.Authentication.Certificate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES_EDWS.Models;

namespace MES_EDWS.Controllers
{
    /// <summary>
    /// Receives community engagement (CE) verification results from the CEP system via MES-ISS.
    /// ICD Reference: CEP-ICD-003 CEP to EDWS v1.0
    /// </summary>
    [ApiController]
    [Route("api/mes/clientinfo")]
    [Authorize(AuthenticationSchemes = CertificateAuthenticationDefaults.AuthenticationScheme)]
    public class ClientInfoController : ControllerBase
    {
        private readonly ILogger<ClientInfoController> _logger;

        public ClientInfoController(ILogger<ClientInfoController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Accepts a CE verification result payload from CEP for one or more individuals
        /// and returns a synchronous acknowledgement.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ReceiveCeVerificationResults([FromBody] CepDWRequestDTO request)
        {
            _logger.LogInformation(
                "Received CE verification results. NvhRefferenceId: {NvhRefferenceId}, " +
                "RequestSequenceNumber: {RequestSequenceNumber}, StateId: {StateId}, " +
                "RequestSource: {RequestSource}, IndividualCount: {IndividualCount}",
                request.NvhRefferenceId,
                request.RequestSequenceNumber,
                request.StateId,
                request.RequestSource,
                request.NvhResponses.Count);

            // TODO: Persist CE verification results to the data store once the schema is defined.

            var nvhRequestId = GenerateNvhRequestId();

            _logger.LogInformation(
                "CE verification acknowledged. NvhRequestId: {NvhRequestId}, " +
                "NvhRefferenceId: {NvhRefferenceId}",
                nvhRequestId,
                request.NvhRefferenceId);

            var response = new CepDWAckResponseDTO
            {
                RequestSequenceNumber = request.RequestSequenceNumber,
                StateId               = request.StateId,
                RequestSource         = request.RequestSource,
                Acknowledgement       = new AcknowledgementDTO
                {
                    Code             = "REQUEST_CREATED",
                    Status           = "SUCCESS",
                    Message          = "Request has been created successfully",
                    NvhRequestId     = nvhRequestId,
                    CreatedTimestamp = DateTime.UtcNow
                }
            };

            return Ok(response);
        }

        /// <summary>
        /// Generates a short numeric identifier for the NVH request, matching the sample format
        /// shown in the ICD (e.g. "988862").
        /// TODO: Replace with a persisted sequential ID once storage is available.
        /// </summary>
        private static string GenerateNvhRequestId() =>
            Random.Shared.Next(100_000, 999_999).ToString();
    }
}
