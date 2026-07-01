using MES_EDWS.Models;
using Teradata.Client.Provider;

namespace MES_EDWS.Services
{
    public class MedicalFrailtyService : IMedicalFrailtyService
    {
        private readonly string _connectionString;
        private readonly ILogger<MedicalFrailtyService> _logger;

        // TODO: Update with the actual Teradata database/table name once confirmed
        private const string TableName = "your_database.medical_frailty";

        public MedicalFrailtyService(IConfiguration configuration, ILogger<MedicalFrailtyService> logger)
        {
            _connectionString = configuration.GetConnectionString("TeradataConnection")
                ?? throw new InvalidOperationException("TeradataConnection connection string is not configured.");
            _logger = logger;
        }

        public async Task<MedicalFrailtyRecord?> GetByMmisEnrolleeIdOrSsnAsync(string requestId, string mmisEnrolleeId, string? ssn)
        {
            // First attempt: look up by MMIS Enrollee ID
            var record = await QueryAsync(
                $"SELECT mmisEnrolleeId, ssn, medicallyFrail, circumstanceStartDate, circumstanceEndDate FROM {TableName} WHERE mmisEnrolleeId = ?",
                mmisEnrolleeId);

            if (record != null)
            {
                _logger.LogInformation("Medical frailty record found by mmisEnrolleeId: {MmisEnrolleeId}", mmisEnrolleeId);
                return record;
            }

            // Second attempt: fall back to SSN if provided
            if (!string.IsNullOrWhiteSpace(ssn))
            {
                record = await QueryAsync(
                    $"SELECT mmisEnrolleeId, ssn, medicallyFrail, circumstanceStartDate, circumstanceEndDate FROM {TableName} WHERE ssn = ?",
                    ssn);

                if (record != null)
                {
                    _logger.LogInformation("Medical frailty record found by SSN for mmisEnrolleeId: {MmisEnrolleeId}", mmisEnrolleeId);
                    return record;
                }
            }

            _logger.LogWarning(
                "No medical frailty record found for mmisEnrolleeId: {MmisEnrolleeId} or SSN",
                mmisEnrolleeId);

            return null;
        }

        private async Task<MedicalFrailtyRecord?> QueryAsync(string sql, string parameterValue)
        {
            try
            {
                await using var connection = new TdConnection(_connectionString);
                await connection.OpenAsync();

                await using var command = new TdCommand(sql, connection);
                command.Parameters.Add(new TdParameter { Value = parameterValue });

                await using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new MedicalFrailtyRecord
                    {
                        MmisEnrolleeId       = reader["mmisEnrolleeId"]?.ToString(),
                        Ssn                  = reader["ssn"]?.ToString(),
                        MedicallyFrail       = ParseMedicallyFrail(reader["medicallyFrail"]),
                        CircumstanceStartDate = FormatDate(reader["circumstanceStartDate"]),
                        CircumstanceEndDate   = FormatDate(reader["circumstanceEndDate"])
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Teradata query failed. SQL: {Sql}", sql);
                throw;
            }
        }

        private static bool ParseMedicallyFrail(object value)
        {
            if (value == null || value == DBNull.Value) return false;
            var str = value.ToString()?.Trim().ToUpperInvariant();
            return str == "Y" || str == "YES" || str == "TRUE" || str == "1";
        }

        private static string? FormatDate(object value)
        {
            if (value == null || value == DBNull.Value) return null;
            if (value is DateTime dt) return dt.ToString("yyyy-MM-dd");
            var str = value.ToString();
            if (DateTime.TryParse(str, out var parsed)) return parsed.ToString("yyyy-MM-dd");
            return str;
        }
    }
}
