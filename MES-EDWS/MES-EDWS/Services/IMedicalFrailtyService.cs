using MES_EDWS.Models;

namespace MES_EDWS.Services
{
    public interface IMedicalFrailtyService
    {
        /// <summary>
        /// Looks up a member's medical frailty record in HR1_MEDICALLY_FRAIL_MEMBERS.
        /// Queries by MMIS_ENROLLEE_ID first; if no match is found, falls back to SSN.
        /// Returns null if no current record is found by either identifier.
        /// <paramref name="requestId"/> is required: null or blank throws <see cref="ArgumentException"/>.
        /// A provided <paramref name="ssn"/> not in XXX-XX-XXXX format throws <see cref="SsnValidationException"/>.
        /// </summary>
        Task<MedicalFrailtyRecord?> GetByMmisEnrolleeIdOrSsnAsync(
            string requestId, string? mmisEnrolleeId, string? ssn);

        /// <summary>
        /// Persists the inbound API request to HR1_MEDICALLY_FRAIL_REQUEST.
        /// <paramref name="requestId"/> is required: null or blank throws <see cref="ArgumentException"/>.
        /// Database failures are logged but do not abort the caller.
        /// </summary>
        Task SaveRequestAsync(string requestId, string? mmisEnrolleeId, string? ssn);

        /// <summary>
        /// Persists the outbound API response to HR1_MEDICALLY_FRAIL_RESPONSE.
        /// <paramref name="requestId"/> is required: null or blank throws <see cref="ArgumentException"/>.
        /// Database failures are logged but do not abort the caller.
        /// </summary>
        Task SaveResponseAsync(
            string requestId,
            string? medicallyFrail,
            string? circumstanceStartDate,
            string? circumstanceEndDate,
            string? code,
            string? message);

        /// <summary>
        /// Persists one API call to MWRP_CE_MED_FRAIL_API_TRANSACTION, MWRP_CE_MED_FRAIL_API_REQUEST
        /// and MWRP_CE_MED_FRAIL_API_RESPONSE. Failures are logged but do not abort the caller.
        /// </summary>
        Task SaveApiAuditAsync(ApiAudit audit);
    }
}
