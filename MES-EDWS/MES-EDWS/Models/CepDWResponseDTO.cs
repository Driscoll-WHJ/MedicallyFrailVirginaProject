using System.Text.Json.Serialization;

namespace MES_EDWS.Models
{
    // ─── Success Response (HTTP 200) ─────────────────────────────────────────────
    // CEP-003 ICD v1.7.4 "Acknowledgement Response": CepDWAckResponseDTO / AcknowledgementDTO.
    // v1.7.4 removed source / updateSw from both DTOs (and from MWRP_CE_RESPONSE).

    public class CepDWAckResponseDTO
    {
        /// <summary>
        /// Echoed from the request as sent. The ICD types this Integer here but String in the
        /// request; it stays a string so non-numeric values echo back unchanged.
        /// </summary>
        [JsonPropertyName("requestSequenceNumber")]
        public string RequestSequenceNumber { get; set; } = string.Empty;

        [JsonPropertyName("stateId")]
        public string StateId { get; set; } = string.Empty;

        [JsonPropertyName("requestSource")]
        public string RequestSource { get; set; } = string.Empty;

        [JsonPropertyName("acknowledgement")]
        public AcknowledgementDTO Acknowledgement { get; set; } = new();
    }

    public class AcknowledgementDTO
    {
        /// <summary>Always "REQUEST_CREATED" on success.</summary>
        [JsonPropertyName("code")]
        public string Code { get; set; } = "REQUEST_CREATED";

        /// <summary>Always "SUCCESS" on success.</summary>
        [JsonPropertyName("status")]
        public string Status { get; set; } = "SUCCESS";

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Unique identifier for this request, used for response correlation (was "nvhRequestId"
        /// before ICD v1.7.3). Persisted to MWRP_CE_RESPONSE.MWRP_CE_REQUEST_ID.
        /// </summary>
        [JsonPropertyName("requestId")]
        public string RequestId { get; set; } = string.Empty;

        /// <summary>Not in the ICD AcknowledgementDTO table; kept for existing consumers and MWRP_CE_RESPONSE.CREATED_TS.</summary>
        [JsonPropertyName("createdTimestamp")]
        public DateTime CreatedTimestamp { get; set; }
    }

    // ─── Error Response (HTTP 400 / 422 / 500) ───────────────────────────────────
    // CEP-003 ICD v1.7.4 "Error Response": CepDWResultDTO / ErrorMessageDTO /
    // ErrorDetailDTO / InvalidFieldDTO. None of them carry source / updateSw any more.

    public class CepDWResultDTO
    {
        /// <summary>Error occurrence timestamp.</summary>
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonPropertyName("message")]
        public ErrorMessageDTO Message { get; set; } = new();

        /// <summary>Correlation ID for support reference (the per-call TRANSACTION_ID in MWRP_CE_API_*).</summary>
        [JsonPropertyName("correlationId")]
        public string CorrelationId { get; set; } = string.Empty;

        [JsonPropertyName("error")]
        public ErrorDetailDTO Error { get; set; } = new();
    }

    public class ErrorMessageDTO
    {
        /// <summary>Numeric error code, e.g. 101 for validation failure.</summary>
        [JsonPropertyName("code")]
        public int Code { get; set; }

        /// <summary>Always "ERROR".</summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = "ERROR";

        /// <summary>True if the error requires user notification; false for system errors.</summary>
        [JsonPropertyName("notify")]
        public bool Notify { get; set; }
    }

    public class ErrorDetailDTO
    {
        [JsonPropertyName("invalidFields")]
        public List<InvalidFieldDTO> InvalidFields { get; set; } = new();
    }

    public class InvalidFieldDTO
    {
        /// <summary>JSON path of the invalid field, e.g. ceVerified[0].clientId.</summary>
        [JsonPropertyName("field")]
        public string Field { get; set; } = string.Empty;

        /// <summary>Validation rule code that was violated, e.g. Required, Format.</summary>
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }
}
