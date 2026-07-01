using System.Text.Json.Serialization;

namespace MES_EDWS.Models
{
    public class MedicallyFrailResponse
    {
        [JsonPropertyName("requestId")]
        public string RequestId { get; set; } = string.Empty;

        [JsonPropertyName("medicallyFrail")]
        public string MedicallyFrail { get; set; } = string.Empty;

        [JsonPropertyName("circumstanceStartDate")]
        public string? CircumstanceStartDate { get; set; }

        [JsonPropertyName("circumstanceEndDate")]
        public string? CircumstanceEndDate { get; set; }

        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }
}
