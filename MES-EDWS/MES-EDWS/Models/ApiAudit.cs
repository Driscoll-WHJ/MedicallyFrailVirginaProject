namespace MES_EDWS.Models
{
    /// <summary>
    /// One API call, persisted to an API_TRANSACTION / API_REQUEST / API_RESPONSE table trio
    /// (MWRP_CE_MED_FRAIL_API_* for /api/mes/medically-frail, MWRP_CE_API_* for
    /// /api/nvh/verification-request).
    /// </summary>
    public class ApiAudit
    {
        /// <summary>Caller's business identifier (medically frail requestId / CEP requestSequenceNumber).</summary>
        public string RequestId { get; set; } = string.Empty;

        /// <summary>GUID generated per call; shared by the three audit rows so they can be joined.</summary>
        public string TransactionId { get; set; } = string.Empty;

        public string ApiName { get; set; } = string.Empty;
        public string HttpMethod { get; set; } = string.Empty;
        public int StatusCode { get; set; }
        public DateTime RequestTime { get; set; }
        public DateTime ResponseTime { get; set; }
        public string? RequestJson { get; set; }
        public string? ResponseJson { get; set; }
    }
}
