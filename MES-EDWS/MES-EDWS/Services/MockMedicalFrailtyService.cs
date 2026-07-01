using MES_EDWS.Models;

namespace MES_EDWS.Services
{
    /// <summary>
    /// Temporary stub that returns hardcoded sample data keyed by requestId.
    /// Replace registration in Program.cs with MedicalFrailtyService once
    /// Teradata connection details are available.
    /// </summary>
    public class MockMedicalFrailtyService : IMedicalFrailtyService
    {
        private readonly ILogger<MockMedicalFrailtyService> _logger;

        private static readonly Dictionary<string, MedicalFrailtyRecord> SampleData =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["REQ-2026-01"] = new MedicalFrailtyRecord
                {
                    MmisEnrolleeId        = "1000000001",
                    Ssn                   = "111-11-1111",
                    MedicallyFrail        = true,
                    CircumstanceStartDate = "2026-03-01",
                    CircumstanceEndDate   = null
                },
                ["REQ-2026-02"] = new MedicalFrailtyRecord
                {
                    MmisEnrolleeId        = "1000000002",
                    Ssn                   = "222-22-2222",
                    MedicallyFrail        = false,
                    CircumstanceStartDate = "2025-10-05",
                    CircumstanceEndDate   = null
                },
                ["REQ-2026-03"] = new MedicalFrailtyRecord
                {
                    MmisEnrolleeId        = "1000000003",
                    Ssn                   = "333-33-3333",
                    MedicallyFrail        = true,
                    CircumstanceStartDate = "2026-01-01",
                    CircumstanceEndDate   = "2026-08-01"
                }
            };

        public MockMedicalFrailtyService(ILogger<MockMedicalFrailtyService> logger)
        {
            _logger = logger;
        }

        public Task<MedicalFrailtyRecord?> GetByMmisEnrolleeIdOrSsnAsync(
            string requestId, string mmisEnrolleeId, string? ssn)
        {
            if (SampleData.TryGetValue(requestId, out var record))
            {
                _logger.LogInformation(
                    "[MOCK] Medical frailty record found for RequestId: {RequestId}", requestId);
                return Task.FromResult<MedicalFrailtyRecord?>(record);
            }

            _logger.LogWarning(
                "[MOCK] No sample record found for RequestId: {RequestId}", requestId);
            return Task.FromResult<MedicalFrailtyRecord?>(null);
        }
    }
}
