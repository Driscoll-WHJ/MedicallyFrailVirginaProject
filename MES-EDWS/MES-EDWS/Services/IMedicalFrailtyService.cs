using MES_EDWS.Models;

namespace MES_EDWS.Services
{
    public interface IMedicalFrailtyService
    {
        /// <summary>
        /// Looks up a member's medical frailty record.
        /// Queries by mmisEnrolleeId first; if no match is found, falls back to SSN.
        /// Returns null if no record is found by either identifier.
        /// </summary>
        Task<MedicalFrailtyRecord?> GetByMmisEnrolleeIdOrSsnAsync(string requestId, string mmisEnrolleeId, string? ssn);
    }
}
