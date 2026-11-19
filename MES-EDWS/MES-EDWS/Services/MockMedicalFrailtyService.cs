using MES_EDWS.Models;

namespace MES_EDWS.Services
{
    /// <summary>
    /// Temporary stub — returns hardcoded sample data keyed by MmisEnrolleeId.
    /// Swap registration in Program.cs to MedicalFrailtyService for production.
    /// </summary>
    public class MockMedicalFrailtyService : IMedicalFrailtyService
    {
        private readonly ILogger<MockMedicalFrailtyService> _logger;

        private static readonly Dictionary<string, MedicalFrailtyRecord> SampleData =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["1000000001"] = new MedicalFrailtyRecord
                {
                    MmisEnrolleeId        = "1000000001",
                    Ssn                   = "111-11-1111",
                    MedicallyFrail        = true,
                    CircumstanceStartDate = "2026-03-01",
                    CircumstanceEndDate   = null,
                    EdwsCurrentInd        = "Y",
                    EdwsDatasource        = "MOCK"
                },
                ["1000000002"] = new MedicalFrailtyRecord
                {
                    MmisEnrolleeId        = "1000000002",
                    Ssn                   = "222-22-2222",
                    MedicallyFrail        = false,
                    CircumstanceStartDate = "2025-10-05",
                    CircumstanceEndDate   = null,
                    EdwsCurrentInd        = "Y",
                    EdwsDatasource        = "MOCK"
                },
                ["1000000003"] = new MedicalFrailtyRecord
                {
                    MmisEnrolleeId        = "1000000003",
                    Ssn                   = "333-33-3333",
                    MedicallyFrail        = true,
                    CircumstanceStartDate = "2026-01-01",
                    CircumstanceEndDate   = "2026-08-01",
                    EdwsCurrentInd        = "Y",
                    EdwsDatasource        = "MOCK"
                }
            };

        public MockMedicalFrailtyService(ILogger<MockMedicalFrailtyService> logger)
        {
            _logger = logger;
        }

        public Task<MedicalFrailtyRecord?> GetByMmisEnrolleeIdOrSsnAsync(
            string requestId, string? mmisEnrolleeId, string? ssn)
        {
            if (!string.IsNullOrWhiteSpace(mmisEnrolleeId) &&
                SampleData.TryGetValue(mmisEnrolleeId, out var record))
            {
                _logger.LogInformation(
                    "[MOCK] Record found by MmisEnrolleeId: {MmisEnrolleeId}", mmisEnrolleeId);
                return Task.FromResult<MedicalFrailtyRecord?>(record);
            }

            // SSN fallback
            if (!string.IsNullOrWhiteSpace(ssn))
            {
                var bySsn = SampleData.Values.FirstOrDefault(r =>
                    string.Equals(r.Ssn, ssn, StringComparison.OrdinalIgnoreCase));

                if (bySsn != null)
                {
                    _logger.LogInformation("[MOCK] Record found by SSN for RequestId: {RequestId}", requestId);
                    return Task.FromResult<MedicalFrailtyRecord?>(bySsn);
                }
            }

            _logger.LogWarning(
                "[MOCK] No sample record for MmisEnrolleeId: {MmisEnrolleeId}", mmisEnrolleeId);
            return Task.FromResult<MedicalFrailtyRecord?>(null);
        }

        public Task SaveRequestAsync(string requestId, string? mmisEnrolleeId, string? ssn)
        {
            _logger.LogInformation(
                "[MOCK] SaveRequest — RequestId: {RequestId}, MmisEnrolleeId: {MmisEnrolleeId}",
                requestId, mmisEnrolleeId);
            return Task.CompletedTask;
        }

        public Task SaveResponseAsync(
            string requestId,
            string medicallyFrail,
            string? circumstanceStartDate,
            string? circumstanceEndDate,
            string? errorCode,
            string? errorMessage)
        {
            _logger.LogInformation(
                "[MOCK] SaveResponse — RequestId: {RequestId}, MedicallyFrail: {MedicallyFrail}",
                requestId, medicallyFrail);
            return Task.CompletedTask;
        }
    }
}
