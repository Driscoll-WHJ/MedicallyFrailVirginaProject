using MES_EDWS.Models;

namespace MES_EDWS.Services
{
    public interface IClientInfoService
    {
        /// <summary>
        /// Parses a CE verification result payload (CEP-ICD-003) and persists it to the
        /// HR1_MWR_* Teradata tables inside a single transaction. All child records
        /// (individuals, exemptions, employers, employments, pay statements, annual income,
        /// job training, volunteering, education and reference documents) are inserted
        /// with generated ids and sequence numbers.
        /// Returns the generated NVH_REQUEST_ID used for the acknowledgement.
        /// </summary>
        Task<string> SaveCeVerificationResultsAsync(CepDWRequestDTO request);
    }
}
