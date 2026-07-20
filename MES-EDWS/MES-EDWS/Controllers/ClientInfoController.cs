using Microsoft.AspNetCore.Authentication.Certificate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES_EDWS.Models;
using MES_EDWS.Services;

namespace MES_EDWS.Controllers
{
    /// <summary>
    /// Receives community engagement (CE) verification results from the CEP system via MES-ISS.
    /// ICD Reference: CEP-ICD-003 CEP to EDWS v1.0
    /// </summary>
    [ApiController]
    [Route("api/nvh/verification-requests")]
    [Authorize(AuthenticationSchemes = CertificateAuthenticationDefaults.AuthenticationScheme)]
    public class ClientInfoController : ControllerBase
    {
        private readonly ILogger<ClientInfoController> _logger;
        private readonly IClientInfoService _clientInfoService;

        public ClientInfoController(
            ILogger<ClientInfoController> logger,
            IClientInfoService clientInfoService)
        {
            _logger = logger;
            _clientInfoService = clientInfoService;
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

            // Parse the payload and persist it to the HR1_MWR_* Teradata tables.
            var nvhRequestId = await _clientInfoService.SaveCeVerificationResultsAsync(request);

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
    }
}
