using MES_EDWS.Models;

namespace MES_EDWS.Services
{
    public interface IClientInfoService
    {
        /// <summary>
        /// Parses a CE verification result payload (CEP-ICD-003) and persists it to the
        /// HR1_DMAS_POC.MWRP_CE_* Teradata tables inside a single transaction. All child
        /// records (verified member, addresses, exclusions, exceptions, employment,
        /// job training, education, volunteering and Truv-verified employment) are
        /// inserted with generated surrogate keys.
        /// Returns the acknowledgement response that was also persisted to MWRP_CE_RESPONSE.
        /// </summary>
        Task<CepDWAckResponseDTO> SaveCeVerificationResultsAsync(CepDWRequestDTO request);
    }
}
