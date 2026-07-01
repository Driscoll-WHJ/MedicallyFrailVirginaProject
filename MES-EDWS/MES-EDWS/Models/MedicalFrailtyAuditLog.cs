using System.ComponentModel.DataAnnotations;

namespace MES_EDWS.Models
{
    public class MedicalFrailtyAuditLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string RequestId { get; set; } = string.Empty;

        [Required]
        public DateTime DateRequested { get; set; }

        [Required]
        public string MmisEnrolleeId { get; set; } = string.Empty;

        public string? Ssn { get; set; }

        public bool MedicallyFrail { get; set; }

        public string? CircumstanceStartDate { get; set; }
    }
}
