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
    [Route("api/nvh/verification-request")]
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
                "Received CE verification results. CaseNumber: {CaseNumber}, " +
                "RequestSequenceNumber: {RequestSequenceNumber}, State: {State}, " +
                "RequestSource: {RequestSource}, ClientIdentificationNumber: {ClientIdentificationNumber}",
                request.CaseNumber,
                request.RequestSequenceNumber,
                request.State,
                request.RequestSource,
                request.CeVerified.Count > 0 ? request.CeVerified[0].ClientIdentificationNumber : null);

            // Persists both the request (MWRP_CE_REQUEST) and this same response (MWRP_CE_RESPONSE)
            // to the HR1_DMAS_POC.MWRP_CE_* Teradata tables inside one transaction.
            var response = await _clientInfoService.SaveCeVerificationResultsAsync(request);

            _logger.LogInformation(
                "CE verification acknowledged. NvhRequestId: {NvhRequestId}, CaseNumber: {CaseNumber}",
                response.Acknowledgement.NvhRequestId,
                request.CaseNumber);

            return Ok(response);
        }
    }
}
