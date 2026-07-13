using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MES_EDWS.Models
{
    // ─── Success Response (HTTP 200) ─────────────────────────────────────────────

    public class CepDWAckResponseDTO
    {
        [JsonPropertyName("requestSequenceNumber")]
        public int RequestSequenceNumber { get; set; }

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

        /// <summary>EDWS-assigned unique identifier for this NVH request. CEP uses this for response correlation.</summary>
        [JsonPropertyName("nvhRequestId")]
        public string NvhRequestId { get; set; } = string.Empty;

        [JsonPropertyName("createdTimestamp")]
        public DateTime CreatedTimestamp { get; set; }
    }

    // ─── Error Response (HTTP 400 / 422) ─────────────────────────────────────────

    public class CepDWErrorDTO
    {
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonPropertyName("message")]
        public ErrorMessageDTO Message { get; set; } = new();

        [JsonPropertyName("correlationId")]
        public string CorrelationId { get; set; } = string.Empty;

        [JsonPropertyName("error")]
        public ErrorDetailDTO Error { get; set; } = new();
    }

    public class ErrorMessageDTO
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

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
        [JsonPropertyName("field")]
        public string Field { get; set; } = string.Empty;

        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }
}
