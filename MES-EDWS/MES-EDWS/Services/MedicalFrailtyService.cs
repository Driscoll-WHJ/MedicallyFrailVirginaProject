using MES_EDWS.Models;
using Teradata.Client.Provider;

namespace MES_EDWS.Services
{
    public class MedicalFrailtyService : IMedicalFrailtyService
    {
        private readonly string _connectionString;
        private readonly ILogger<MedicalFrailtyService> _logger;

        private const string DataSource = "MES-EDWS";

        // ── Table names ────────────────────────────────────────────────────────────
        private const string MembersTable  = "HR1_DMAS_POC.HR1_MEDICALLY_FRAIL_MEMBERS_DEV";
        private const string RequestTable  = "HR1_DMAS_POC.HR1_MEDICALLY_FRAIL_REQUEST_DEV";
        private const string ResponseTable = "HR1_DMAS_POC.HR1_MEDICALLY_FRAIL_RESPONSE_DEV";

        // ── HR1_MEDICALLY_FRAIL_MEMBERS columns ────────────────────────────────────
        private const string MCol_MmisEnrolleeId   = "MMIS_ENROLLEE_ID";
        private const string MCol_Ssn               = "SSN";
        private const string MCol_MedicallyFrailFlag = "MEDICALLY_FRAIL_FLAG";
        private const string MCol_CircumStartDate   = "CIRCUMSTANCE_START_DATE";
        private const string MCol_CircumEndDate     = "CIRCUMSTANCE_END_DATE";
        private const string MCol_CurrentInd        = "EDWS_CURRENT_IND";
        private const string MCol_Datasource        = "EDWS_DATASOURCE";
        private const string MCol_DateInsert        = "EDWS_DATE_INSERT";

        // ── HR1_MEDICALLY_FRAIL_REQUEST columns ────────────────────────────────────
        private const string RqCol_RequestId      = "REQUEST_ID";
        private const string RqCol_MmisEnrolleeId = "MMIS_ENROLLEE_ID";
        private const string RqCol_Ssn            = "SSN";
        private const string RqCol_CurrentInd     = "EDWS_CURRENT_IND";
        private const string RqCol_Datasource     = "EDWS_DATASOURCE";
        private const string RqCol_DateInsert     = "EDWS_DATE_INSERT";

        // ── HR1_MEDICALLY_FRAIL_RESPONSE columns ───────────────────────────────────
        private const string RsCol_RequestId         = "REQUEST_ID";
        private const string RsCol_MedicallyFrail    = "MEDICALLY_FRAIL";
        private const string RsCol_CircumStartDate   = "CIRCUMSTANCE_START_DATE";
        private const string RsCol_CircumEndDate     = "CIRCUMSTANCE_END_DATE";
        private const string RsCol_ErrorCode         = "ERROR_CODE";
        private const string RsCol_ErrorMessage      = "ERROR_MESSAGE";
        private const string RsCol_CurrentInd        = "EDWS_CURRENT_IND";
        private const string RsCol_Datasource        = "EDWS_DATASOURCE";
        private const string RsCol_DateInsert        = "EDWS_DATE_INSERT";

        public MedicalFrailtyService(IConfiguration configuration, ILogger<MedicalFrailtyService> logger)
        {
            _connectionString = configuration.GetConnectionString("TeradataConnection")
                ?? throw new InvalidOperationException("TeradataConnection connection string is not configured.");
            _logger = logger;
        }

        // ── Lookup ─────────────────────────────────────────────────────────────────

        public async Task<MedicalFrailtyRecord?> GetByMmisEnrolleeIdOrSsnAsync(
            string requestId, string? mmisEnrolleeId, string? ssn)
        {
            // Primary lookup: MMIS_ENROLLEE_ID (only when a value is present in the payload)
            if (!string.IsNullOrWhiteSpace(mmisEnrolleeId))
            {
                var byMmis = await QueryMembersAsync(MCol_MmisEnrolleeId, mmisEnrolleeId);

                if (byMmis != null)
                {
                    _logger.LogInformation(
                        "Medical frailty record found by MMIS_ENROLLEE_ID for RequestId: {RequestId}", requestId);
                    return byMmis;
                }

                _logger.LogInformation(
                    "No match by MMIS_ENROLLEE_ID for RequestId: {RequestId} — falling back to SSN", requestId);
            }

            // SSN lookup — used when MMIS ID was absent or returned no match
            if (!string.IsNullOrWhiteSpace(ssn))
            {
                var bySsn = await QueryMembersAsync(MCol_Ssn, ssn);

                if (bySsn != null)
                {
                    _logger.LogInformation(
                        "Medical frailty record found by SSN for RequestId: {RequestId}", requestId);
                    return bySsn;
                }
            }

            _logger.LogWarning(
                "No medical frailty record found for RequestId: {RequestId}, MmisEnrolleeId: {MmisEnrolleeId}",
                requestId, mmisEnrolleeId);

            return null;
        }

        private async Task<MedicalFrailtyRecord?> QueryMembersAsync(string column, string value)
        {
            var sql =
                $"SELECT {MCol_MmisEnrolleeId}, {MCol_Ssn}, {MCol_MedicallyFrailFlag}, " +
                $"{MCol_CircumStartDate}, {MCol_CircumEndDate}, " +
                $"{MCol_CurrentInd}, {MCol_Datasource}, {MCol_DateInsert} " +
                $"FROM {MembersTable} " +
                $"WHERE {column} = ? AND {MCol_CurrentInd} = 'Y'";

            try
            {
                await using var connection = new TdConnection(_connectionString);
                await connection.OpenAsync();

                await using var command = new TdCommand(sql, connection);
                command.Parameters.Add(new TdParameter { Value = value });

                await using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new MedicalFrailtyRecord
                    {
                        MmisEnrolleeId        = reader[MCol_MmisEnrolleeId]?.ToString(),
                        Ssn                   = reader[MCol_Ssn]?.ToString(),
                        MedicallyFrail        = IsYes(reader[MCol_MedicallyFrailFlag]),
                        CircumstanceStartDate = FormatDate(reader[MCol_CircumStartDate]),
                        CircumstanceEndDate   = FormatDate(reader[MCol_CircumEndDate]),
                        EdwsCurrentInd        = reader[MCol_CurrentInd]?.ToString(),
                        EdwsDatasource        = reader[MCol_Datasource]?.ToString(),
                        EdwsDateInsert        = FormatDate(reader[MCol_DateInsert])
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Teradata members query failed. Column: {Column}, Value: {Value}", column, value);
                throw;
            }
        }

        // ── Request logging ────────────────────────────────────────────────────────

        public async Task SaveRequestAsync(string requestId, string? mmisEnrolleeId, string? ssn)
        {
            var sql =
                $"INSERT INTO {RequestTable} " +
                $"({RqCol_RequestId}, {RqCol_MmisEnrolleeId}, {RqCol_Ssn}, " +
                $"{RqCol_CurrentInd}, {RqCol_Datasource}, {RqCol_DateInsert}) " +
                $"VALUES (?, ?, ?, 'Y', ?, ?)";

            try
            {
                await using var connection = new TdConnection(_connectionString);
                await connection.OpenAsync();

                await using var command = new TdCommand(sql, connection);
                command.Parameters.Add(new TdParameter { Value = requestId });
                command.Parameters.Add(new TdParameter { Value = (object?)mmisEnrolleeId ?? DBNull.Value });
                command.Parameters.Add(new TdParameter { Value = (object?)ssn ?? DBNull.Value });
                command.Parameters.Add(new TdParameter { Value = DataSource });
                command.Parameters.Add(new TdParameter { Value = DateTime.Today });

                await command.ExecuteNonQueryAsync();

                _logger.LogInformation("Request saved to Teradata for RequestId: {RequestId}", requestId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save request to Teradata for RequestId: {RequestId}", requestId);
            }
        }

        // ── Response logging ───────────────────────────────────────────────────────

        public async Task SaveResponseAsync(
            string requestId,
            string medicallyFrail,
            string? circumstanceStartDate,
            string? circumstanceEndDate,
            string? errorCode,
            string? errorMessage)
        {
            var sql =
                $"INSERT INTO {ResponseTable} " +
                $"({RsCol_RequestId}, {RsCol_MedicallyFrail}, " +
                $"{RsCol_CircumStartDate}, {RsCol_CircumEndDate}, " +
                $"{RsCol_ErrorCode}, {RsCol_ErrorMessage}, " +
                $"{RsCol_CurrentInd}, {RsCol_Datasource}, {RsCol_DateInsert}) " +
                $"VALUES (?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            try
            {
                await using var connection = new TdConnection(_connectionString);
                await connection.OpenAsync();

                await using var command = new TdCommand(sql, connection);
                command.Parameters.Add(new TdParameter { Value = requestId });
                command.Parameters.Add(new TdParameter { Value = medicallyFrail });
                command.Parameters.Add(new TdParameter { Value = ToDbDate(circumstanceStartDate) });
                command.Parameters.Add(new TdParameter { Value = ToDbDate(circumstanceEndDate) });
                command.Parameters.Add(new TdParameter { Value = (object?)errorCode    ?? DBNull.Value });
                command.Parameters.Add(new TdParameter { Value = (object?)errorMessage ?? DBNull.Value });
                command.Parameters.Add(new TdParameter { Value = DataSource });
                command.Parameters.Add(new TdParameter { Value = DateTime.Today });

                await command.ExecuteNonQueryAsync();

                _logger.LogInformation("Response saved to Teradata for RequestId: {RequestId}", requestId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save response to Teradata for RequestId: {RequestId}", requestId);
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────────────

        private static bool IsYes(object value)
        {
            if (value == null || value == DBNull.Value) return false;
            return value.ToString()?.Trim().ToUpperInvariant() == "Y";
        }

        private static string? FormatDate(object value)
        {
            if (value == null || value == DBNull.Value) return null;
            if (value is DateTime dt)   return dt.ToString("yyyy-MM-dd");
            if (value is DateOnly d)    return d.ToString("yyyy-MM-dd");
            var str = value.ToString();
            if (DateTime.TryParse(str, out var parsed)) return parsed.ToString("yyyy-MM-dd");
            return str;
        }

        private static object ToDbDate(string? dateStr)
        {
            if (string.IsNullOrWhiteSpace(dateStr)) return DBNull.Value;
            if (DateTime.TryParse(dateStr, out var dt)) return dt.Date;
            return DBNull.Value;
        }
    }
}
