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

        /// <summary>
        /// Persists whatever a payload with missing required fields does contain, skipping rows
        /// whose NOT NULL key is absent. No acknowledgement is written to MWRP_CE_RESPONSE.
        /// </summary>
        Task SavePartialCeVerificationResultsAsync(CepDWRequestDTO request);

        /// <summary>
        /// Persists one API call to MWRP_CE_API_TRANSACTION, MWRP_CE_API_REQUEST and
        /// MWRP_CE_API_RESPONSE. Failures are logged but do not abort the caller.
        /// </summary>
        Task SaveApiAuditAsync(ApiAudit audit);
    }
}
