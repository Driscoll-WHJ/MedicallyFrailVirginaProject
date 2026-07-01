namespace MES_EDWS.Models
{
    public class MedicalFrailtyRecord
    {
        public string? MmisEnrolleeId { get; set; }
        public string? Ssn { get; set; }
        public bool MedicallyFrail { get; set; }
        public string? CircumstanceStartDate { get; set; }
        public string? CircumstanceEndDate { get; set; }
    }
}
