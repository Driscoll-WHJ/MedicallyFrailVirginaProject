using System.ComponentModel.DataAnnotations;

namespace MES_EDWS.Models
{
    public class MedicallyFrailRequest
    {
        /// <summary>Required; must not be null or an empty string.</summary>
        [Required]
        public string RequestId { get; set; } = string.Empty;
        public string? MmisEnrolleeId { get; set; }
        public string? Ssn { get; set; }
    }
}
