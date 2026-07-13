using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MES_EDWS.Models
{
    // ─── Root Request ────────────────────────────────────────────────────────────

    public class CepDWRequestDTO
    {
        [Required]
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

        [Required]
        [JsonPropertyName("requestSequenceNumber")]
        public int RequestSequenceNumber { get; set; }

        [Required]
        [JsonPropertyName("stateId")]
        public string StateId { get; set; } = string.Empty;

        [Required]
        [JsonPropertyName("requestSource")]
        public string RequestSource { get; set; } = string.Empty;

        // Intentional double-r typo — field name matches the ICD payload spec exactly.
        [Required]
        [JsonPropertyName("nvhRefferenceId")]
        public string NvhRefferenceId { get; set; } = string.Empty;

        [Required]
        [JsonPropertyName("nvhResponses")]
        public List<NvhIndividualResponseDTO> NvhResponses { get; set; } = new();
    }

    // ─── Individual Response ─────────────────────────────────────────────────────

    public class NvhIndividualResponseDTO
    {
        // ICD table uses "indvRefId"; the sample JSON payload uses "nvhIndvRefId" — payload wins.
        [Required]
        [JsonPropertyName("nvhIndvRefId")]
        public int NvhIndvRefId { get; set; }

        [Required]
        [JsonPropertyName("ceVerified")]
        public CeVerifiedDTO CeVerified { get; set; } = new();
    }

    // ─── CE Verification ─────────────────────────────────────────────────────────

    public class CeVerifiedDTO
    {
        [Required]
        [JsonPropertyName("exempt")]
        public string Exempt { get; set; } = string.Empty;

        [Required]
        [JsonPropertyName("complaint")]
        public string Complaint { get; set; } = string.Empty;

        /// <summary>Present when Exempt = "Y".</summary>
        [JsonPropertyName("exemptions")]
        public List<ExemptionResultDTO>? Exemptions { get; set; }

        /// <summary>Present when Complaint = "Y".</summary>
        [JsonPropertyName("engagements")]
        public CeEngagementsDTO? Engagements { get; set; }
    }

    // ─── Exemption ───────────────────────────────────────────────────────────────

    public class ExemptionResultDTO
    {
        // Standard (complaint=Y) path uses circumstanceCode/startDate/documents.
        // Exempt (exempt=Y) path may use reason/exemptionStartDate/supportingDocuments instead.
        // All variants are included so either payload shape deserializes cleanly.

        [JsonPropertyName("circumstanceCode")]
        public string? CircumstanceCode { get; set; }

        [JsonPropertyName("circumstanceDescription")]
        public string? CircumstanceDescription { get; set; }

        [JsonPropertyName("startDate")]
        public string? StartDate { get; set; }

        [JsonPropertyName("endDate")]
        public string? EndDate { get; set; }

        [JsonPropertyName("onGoingPermanent")]
        public bool? OnGoingPermanent { get; set; }

        [JsonPropertyName("documents")]
        public List<DocumentRefDTO>? Documents { get; set; }

        // ── Alternate field names used in the exempt=Y path ──

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }

        [JsonPropertyName("exemptionStartDate")]
        public string? ExemptionStartDate { get; set; }

        [JsonPropertyName("exemptionEndDate")]
        public string? ExemptionEndDate { get; set; }

        [JsonPropertyName("supportingDocuments")]
        public List<DocumentRefDTO>? SupportingDocuments { get; set; }
    }

    public class DocumentRefDTO
    {
        [JsonPropertyName("documentType")]
        public string? DocumentType { get; set; }

        [JsonPropertyName("fileName")]
        public string? FileName { get; set; }
    }

    // ─── CE Engagements ──────────────────────────────────────────────────────────

    public class CeEngagementsDTO
    {
        [JsonPropertyName("verificationSource")]
        public string? VerificationSource { get; set; }

        [JsonPropertyName("employment")]
        public EmploymentVerifiedDTO? Employment { get; set; }

        [JsonPropertyName("jobTraining")]
        public List<JobTrainingResultDTO>? JobTraining { get; set; }

        [JsonPropertyName("education")]
        public EducationVerifiedDTO? Education { get; set; }

        [JsonPropertyName("volunteering")]
        public List<VolunteeringResultDTO>? Volunteering { get; set; }
    }

    // ─── Employment (Truv Payroll) ────────────────────────────────────────────────

    public class EmploymentVerifiedDTO
    {
        [JsonPropertyName("employers")]
        public List<EmployerRecordDTO>? Employers { get; set; }
    }

    public class EmployerRecordDTO
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("product_type")]
        public string? ProductType { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("data_source")]
        public string? DataSource { get; set; }

        [JsonPropertyName("company_name")]
        public string? CompanyName { get; set; }

        [JsonPropertyName("provider")]
        public ProviderDTO? Provider { get; set; }

        [JsonPropertyName("is_suspicious")]
        public bool? IsSuspicious { get; set; }

        [JsonPropertyName("employments")]
        public List<EmploymentDetailDTO>? Employments { get; set; }
    }

    public class ProviderDTO
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("logo_url")]
        public string? LogoUrl { get; set; }
    }

    public class EmploymentDetailDTO
    {
        [JsonPropertyName("job_title")]
        public string? JobTitle { get; set; }

        /// <summary>F = Full-time, P = Part-time.</summary>
        [JsonPropertyName("job_type")]
        public string? JobType { get; set; }

        [JsonPropertyName("start_date")]
        public string? StartDate { get; set; }

        [JsonPropertyName("end_date")]
        public string? EndDate { get; set; }

        [JsonPropertyName("is_active")]
        public bool? IsActive { get; set; }

        [JsonPropertyName("income")]
        public string? Income { get; set; }

        /// <summary>YEARLY, MONTHLY, or HOURLY.</summary>
        [JsonPropertyName("income_unit")]
        public string? IncomeUnit { get; set; }

        [JsonPropertyName("pay_rate")]
        public string? PayRate { get; set; }

        /// <summary>BW = Bi-weekly, W = Weekly, M = Monthly.</summary>
        [JsonPropertyName("pay_frequency")]
        public string? PayFrequency { get; set; }

        [JsonPropertyName("profile")]
        public EmployeeProfileDTO? Profile { get; set; }

        [JsonPropertyName("statements")]
        public List<PayStatementDTO>? Statements { get; set; }

        [JsonPropertyName("annual_income_summary")]
        public List<AnnualIncomeDTO>? AnnualIncomeSummary { get; set; }
    }

    public class EmployeeProfileDTO
    {
        [JsonPropertyName("first_name")]
        public string? FirstName { get; set; }

        [JsonPropertyName("last_name")]
        public string? LastName { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("ssn")]
        public string? Ssn { get; set; }
    }

    public class PayStatementDTO
    {
        [JsonPropertyName("pay_date")]
        public string? PayDate { get; set; }

        [JsonPropertyName("gross_pay")]
        public string? GrossPay { get; set; }

        [JsonPropertyName("net_pay")]
        public string? NetPay { get; set; }
    }

    public class AnnualIncomeDTO
    {
        [JsonPropertyName("year")]
        public int? Year { get; set; }

        [JsonPropertyName("income")]
        public string? Income { get; set; }
    }

    // ─── Job Training ─────────────────────────────────────────────────────────────

    public class JobTrainingResultDTO
    {
        [JsonPropertyName("organizationID")]
        public string? OrganizationId { get; set; }

        [JsonPropertyName("organizationName")]
        public string? OrganizationName { get; set; }

        [JsonPropertyName("hours")]
        public decimal? Hours { get; set; }

        [JsonPropertyName("startDate")]
        public string? StartDate { get; set; }

        [JsonPropertyName("endDate")]
        public string? EndDate { get; set; }

        /// <summary>Reporting period in MM/YYYY format.</summary>
        [JsonPropertyName("effectivePeriod")]
        public string? EffectivePeriod { get; set; }

        [JsonPropertyName("documents")]
        public List<DocumentRefDTO>? Documents { get; set; }
    }

    // ─── Volunteering ─────────────────────────────────────────────────────────────

    public class VolunteeringResultDTO
    {
        [JsonPropertyName("organizationID")]
        public string? OrganizationId { get; set; }

        [JsonPropertyName("organizationName")]
        public string? OrganizationName { get; set; }

        [JsonPropertyName("hours")]
        public decimal? Hours { get; set; }

        [JsonPropertyName("startDate")]
        public string? StartDate { get; set; }

        [JsonPropertyName("endDate")]
        public string? EndDate { get; set; }

        /// <summary>Reporting period in MM/YYYY format.</summary>
        [JsonPropertyName("effectivePeriod")]
        public string? EffectivePeriod { get; set; }

        [JsonPropertyName("documents")]
        public List<DocumentRefDTO>? Documents { get; set; }
    }

    // ─── Education ────────────────────────────────────────────────────────────────

    public class EducationVerifiedDTO
    {
        [JsonPropertyName("electronicallyVerifiedData")]
        public List<NscEnrollmentDTO>? ElectronicallyVerifiedData { get; set; }

        [JsonPropertyName("nonElectronicallyVerifiedData")]
        public List<EducationManualDTO>? NonElectronicallyVerifiedData { get; set; }
    }

    public class NscEnrollmentDTO
    {
        /// <summary>Electronic source identifier, e.g. "NSC".</summary>
        [JsonPropertyName("electronicSource")]
        public string? ElectronicSource { get; set; }

        [JsonPropertyName("officialSchoolName")]
        public string? OfficialSchoolName { get; set; }

        [JsonPropertyName("schoolCode")]
        public string? SchoolCode { get; set; }

        [JsonPropertyName("branchCode")]
        public string? BranchCode { get; set; }

        /// <summary>e.g. "CN" = Continued enrollment.</summary>
        [JsonPropertyName("currentEnrollmentStatus")]
        public string? CurrentEnrollmentStatus { get; set; }

        [JsonPropertyName("enrollmentData")]
        public List<EnrollmentPeriodDTO>? EnrollmentData { get; set; }
    }

    public class EnrollmentPeriodDTO
    {
        [JsonPropertyName("termStartDate")]
        public string? TermStartDate { get; set; }

        [JsonPropertyName("termEndDate")]
        public string? TermEndDate { get; set; }

        [JsonPropertyName("anticipatedGraduationDate")]
        public string? AnticipatedGraduationDate { get; set; }
    }

    public class EducationManualDTO
    {
        [JsonPropertyName("schoolName")]
        public string? SchoolName { get; set; }

        [JsonPropertyName("enrollmentStatus")]
        public string? EnrollmentStatus { get; set; }

        [JsonPropertyName("startDate")]
        public string? StartDate { get; set; }

        [JsonPropertyName("endDate")]
        public string? EndDate { get; set; }

        [JsonPropertyName("documents")]
        public List<DocumentRefDTO>? Documents { get; set; }
    }
}
