namespace MES_EDWS.Models
{
    public class MedicallyFrailRequest
    {
        public string RequestId { get; set; } = string.Empty;
        public string? MmisEnrolleeId { get; set; }
        public string? Ssn { get; set; }
    }
}
