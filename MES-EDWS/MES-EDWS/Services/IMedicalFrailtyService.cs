using MES_EDWS.Models;

namespace MES_EDWS.Services
{
    public interface IMedicalFrailtyService
    {
        /// <summary>
        /// Looks up a member's medical frailty record in HR1_MEDICALLY_FRAIL_MEMBERS.
        /// Queries by MMIS_ENROLLEE_ID first; if no match is found, falls back to SSN.
        /// Returns null if no current record is found by either identifier.
        /// </summary>
        Task<MedicalFrailtyRecord?> GetByMmisEnrolleeIdOrSsnAsync(
            string requestId, string? mmisEnrolleeId, string? ssn);

        /// <summary>
        /// Persists the inbound API request to HR1_MEDICALLY_FRAIL_REQUEST.
        /// Failures are logged but do not abort the caller.
        /// </summary>
        Task SaveRequestAsync(string requestId, string? mmisEnrolleeId, string? ssn);

        /// <summary>
        /// Persists the outbound API response to HR1_MEDICALLY_FRAIL_RESPONSE.
        /// Failures are logged but do not abort the caller.
        /// </summary>
        Task SaveResponseAsync(
            string requestId,
            string medicallyFrail,
            string? circumstanceStartDate,
            string? circumstanceEndDate,
            string? errorCode,
            string? errorMessage);
    }
}
