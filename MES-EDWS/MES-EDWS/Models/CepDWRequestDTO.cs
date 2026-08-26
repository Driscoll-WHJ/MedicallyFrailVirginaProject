using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MES_EDWS.Models
{
    // ─── Root Request ────────────────────────────────────────────────────────────
    // Shape rebuilt to match docs/PostDataPayload.json exactly (CEP-ICD-003) and to
    // feed the HR1_DMAS_POC.MWRP_CE_* tables described in docs/TableDefinition.txt.

    public class CepDWRequestDTO
    {
        [Required]
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

        [Required]
        [JsonPropertyName("requestSequenceNumber")]
        public string RequestSequenceNumber { get; set; } = string.Empty;

        [Required]
        [JsonPropertyName("state")]
        public string State { get; set; } = string.Empty;

        [Required]
        [JsonPropertyName("requestSource")]
        public string RequestSource { get; set; } = string.Empty;

        [JsonPropertyName("caseNumber")]
        public string? CaseNumber { get; set; }

        [JsonPropertyName("sendEmailToCustomer")]
        public bool? SendEmailToCustomer { get; set; }

        [JsonPropertyName("preferredCommunicationLanguage")]
        public string? PreferredCommunicationLanguage { get; set; }

        [JsonPropertyName("documentsUploadedInMWRP")]
        public List<DocumentUploadDTO>? DocumentsUploadedInMwrp { get; set; }

        [Required]
        [JsonPropertyName("ceVerified")]
        public CeVerifiedDTO CeVerified { get; set; } = new();
    }

    // ─── Documents (CE_DOCUMENT) ─────────────────────────────────────────────────
    // Field names inferred from TableDefinition.txt — the sample payload's array is
    // empty, so verify these against the CEP ICD before relying on them in production.

    public class DocumentUploadDTO
    {
        [JsonPropertyName("documentType")]
        public string? DocumentType { get; set; }

        [JsonPropertyName("documentSubtype")]
        public string? DocumentSubtype { get; set; }

        [JsonPropertyName("clientId")]
        public long? ClientId { get; set; }

        [JsonPropertyName("documentId")]
        public long? DocumentId { get; set; }

        [JsonPropertyName("documentUploadTimestamp")]
        public DateTime? DocumentUploadTimestamp { get; set; }

        [JsonPropertyName("documentDeletedTimestamp")]
        public DateTime? DocumentDeletedTimestamp { get; set; }

        [JsonPropertyName("sourceSystem")]
        public string? SourceSystem { get; set; }

        [JsonPropertyName("documentStatus")]
        public string? DocumentStatus { get; set; }
    }

    // ─── CE Verification (CE_VERIFIED) ───────────────────────────────────────────

    public class CeVerifiedDTO
    {
        [JsonPropertyName("clientIdentificationNumber")]
        public string? ClientIdentificationNumber { get; set; }

        [JsonPropertyName("socialSecurityNumber")]
        public string? SocialSecurityNumber { get; set; }

        [JsonPropertyName("mwrStatus")]
        public string? MwrStatus { get; set; }

        [JsonPropertyName("mwrStartDate")]
        public string? MwrStartDate { get; set; }

        [JsonPropertyName("mwrEndDate")]
        public string? MwrEndDate { get; set; }

        /// <summary>No corresponding column in TableDefinition.txt — parsed but not persisted.</summary>
        [JsonPropertyName("lookbackMonth")]
        public string? LookbackMonth { get; set; }

        /// <summary>No matching table yet — persistence pending a dedicated rules-evaluation table.</summary>
        [JsonPropertyName("rulesEvaluationResults")]
        public List<RuleEvaluationResultDTO>? RulesEvaluationResults { get; set; }

        /// <summary>No corresponding column in TableDefinition.txt — parsed but not persisted.</summary>
        [JsonPropertyName("exemptOrExceptionOrExclusionPendingSw")]
        public string? ExemptOrExceptionOrExclusionPendingSw { get; set; }

        [JsonPropertyName("previousEligibilityAuthorizationDate")]
        public string? PreviousEligibilityAuthorizationDate { get; set; }

        [JsonPropertyName("currentEligibilityBeginDate")]
        public string? CurrentEligibilityBeginDate { get; set; }

        [JsonPropertyName("applicationChangeSubmissionDate")]
        public string? ApplicationChangeSubmissionDate { get; set; }

        [JsonPropertyName("renewalInitiatedDate")]
        public string? RenewalInitiatedDate { get; set; }

        [JsonPropertyName("caseAction")]
        public string? CaseAction { get; set; }

        /// <summary>Sample payload only shows a null scalar — modelled as a list since CE_AID_CATEGORY supports multiple rows per member.</summary>
        [JsonPropertyName("aidCategory")]
        public List<AidCategoryDTO>? AidCategory { get; set; }

        [JsonPropertyName("dueDate")]
        public string? DueDate { get; set; }

        [JsonPropertyName("emailVerifiedFlag")]
        public bool? EmailVerifiedFlag { get; set; }

        [JsonPropertyName("primaryModeOfCommunication")]
        public string? PrimaryModeOfCommunication { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("alternateEmail")]
        public string? AlternateEmail { get; set; }

        /// <summary>Sample payload only shows a null scalar — modelled as a list since CE_PHONE supports multiple rows per member.</summary>
        [JsonPropertyName("phoneData")]
        public List<PhoneDataDTO>? PhoneData { get; set; }

        [JsonPropertyName("addresses")]
        public List<AddressDTO>? Addresses { get; set; }

        [JsonPropertyName("headOfHousehold")]
        public bool? HeadOfHousehold { get; set; }

        [JsonPropertyName("requiresCEEvaluation")]
        public bool? RequiresCeEvaluation { get; set; }

        [JsonPropertyName("relationshipWithHOH")]
        public string? RelationshipWithHoh { get; set; }

        [JsonPropertyName("exclusions")]
        public ExclusionsDTO? Exclusions { get; set; }

        [JsonPropertyName("exceptions")]
        public ExceptionsDTO? Exceptions { get; set; }

        [JsonPropertyName("engagements")]
        public EngagementsDTO? Engagements { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updateSw")]
        public string? UpdateSw { get; set; }

        [JsonPropertyName("mmisEnrolleeID")]
        public string? MmisEnrolleeId { get; set; }
    }

    public class RuleEvaluationResultDTO
    {
        [JsonPropertyName("ruleId")]
        public string? RuleId { get; set; }

        [JsonPropertyName("ruleName")]
        public string? RuleName { get; set; }

        [JsonPropertyName("result")]
        public string? Result { get; set; }
    }

    // ─── Aid Category (CE_AID_CATEGORY) ──────────────────────────────────────────
    // Field names inferred from TableDefinition.txt — sample payload shows null.

    public class AidCategoryDTO
    {
        [JsonPropertyName("code")]
        public int? Code { get; set; }

        [JsonPropertyName("startDate")]
        public string? StartDate { get; set; }

        [JsonPropertyName("endDate")]
        public string? EndDate { get; set; }
    }

    // ─── Phone (CE_PHONE) ─────────────────────────────────────────────────────────
    // Field names inferred from TableDefinition.txt — sample payload shows null.

    public class PhoneDataDTO
    {
        [JsonPropertyName("phoneType")]
        public string? PhoneType { get; set; }

        [JsonPropertyName("phoneNumber")]
        public string? PhoneNumber { get; set; }
    }

    // ─── Address (CE_ADDRESS) ─────────────────────────────────────────────────────

    public class AddressDTO
    {
        [JsonPropertyName("addressType")]
        public string? AddressType { get; set; }

        [JsonPropertyName("addressLine1")]
        public string? AddressLine1 { get; set; }

        [JsonPropertyName("addressLine2")]
        public string? AddressLine2 { get; set; }

        [JsonPropertyName("county")]
        public string? County { get; set; }

        [JsonPropertyName("city")]
        public string? City { get; set; }

        [JsonPropertyName("zipCode5")]
        public string? ZipCode5 { get; set; }

        [JsonPropertyName("zipCode4")]
        public string? ZipCode4 { get; set; }

        [JsonPropertyName("state")]
        public string? State { get; set; }

        [JsonPropertyName("fipsCode")]
        public int? FipsCode { get; set; }
    }

    // ─── Exclusions (CE_EXCLUSION_*) ──────────────────────────────────────────────

    public class ExclusionsDTO
    {
        [JsonPropertyName("pregnancy")]
        public PregnancyExclusionDTO? Pregnancy { get; set; }

        [JsonPropertyName("careGiverCircumstance")]
        public CaregiverExclusionDTO? CareGiverCircumstance { get; set; }

        [JsonPropertyName("fosterCare")]
        public FosterCareExclusionDTO? FosterCare { get; set; }

        [JsonPropertyName("formerFosterCare")]
        public FormerFosterCareExclusionDTO? FormerFosterCare { get; set; }

        [JsonPropertyName("incarceratedCircumstance")]
        public IncarcerationExclusionDTO? IncarceratedCircumstance { get; set; }

        [JsonPropertyName("medicarePartAB")]
        public MedicarePartAbExclusionDTO? MedicarePartAB { get; set; }

        [JsonPropertyName("frailty")]
        public FrailtyExclusionDTO? Frailty { get; set; }

        [JsonPropertyName("circumstanceForExclusion")]
        public CircumstanceDTO? CircumstanceForExclusion { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updateSw")]
        public string? UpdateSw { get; set; }
    }

    public class ExceptionsDTO
    {
        [JsonPropertyName("circumstanceForException")]
        public CircumstanceDTO? CircumstanceForException { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updateSw")]
        public string? UpdateSw { get; set; }
    }

    /// <summary>Backs both CE_CIRCUMSTANCE and CE_CIRCUMSTANCE_DETAIL. Field names inferred from TableDefinition.txt — sample payload shows null.</summary>
    public class CircumstanceDTO
    {
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

        [JsonPropertyName("verificationSource")]
        public string? VerificationSource { get; set; }

        [JsonPropertyName("verificationDate")]
        public string? VerificationDate { get; set; }

        [JsonPropertyName("effectiveBeginDate")]
        public string? EffectiveBeginDate { get; set; }

        [JsonPropertyName("effectiveEndDate")]
        public string? EffectiveEndDate { get; set; }

        [JsonPropertyName("americanIndianAlaskanNative")]
        public bool? AmericanIndianAlaskanNative { get; set; }

        [JsonPropertyName("hospitalCare")]
        public bool? HospitalCare { get; set; }

        [JsonPropertyName("extendedMedicalTravel")]
        public bool? ExtendedMedicalTravel { get; set; }

        [JsonPropertyName("substanceUseDisorder")]
        public bool? SubstanceUseDisorder { get; set; }

        [JsonPropertyName("veteran100PercentDisabled")]
        public bool? Veteran100PercentDisabled { get; set; }

        [JsonPropertyName("blindOrDisabled")]
        public bool? BlindOrDisabled { get; set; }

        [JsonPropertyName("tanfEnrolled")]
        public bool? TanfEnrolled { get; set; }

        [JsonPropertyName("snapApproved")]
        public bool? SnapApproved { get; set; }

        [JsonPropertyName("snapWorkReqNotExempt")]
        public bool? SnapWorkReqNotExempt { get; set; }
    }

    public class PregnancyExclusionDTO
    {
        [JsonPropertyName("circumstanceCode")]
        public string? CircumstanceCode { get; set; }

        [JsonPropertyName("expectedDueDate")]
        public string? ExpectedDueDate { get; set; }

        [JsonPropertyName("actualPregnancyEndDate")]
        public string? ActualPregnancyEndDate { get; set; }

        [JsonPropertyName("effectiveBeginDate")]
        public string? EffectiveBeginDate { get; set; }

        [JsonPropertyName("effectiveEndDate")]
        public string? EffectiveEndDate { get; set; }
    }

    public class CaregiverExclusionDTO
    {
        [JsonPropertyName("circumstanceCode")]
        public string? CircumstanceCode { get; set; }

        [JsonPropertyName("relationshipWithDependent")]
        public string? RelationshipWithDependent { get; set; }

        [JsonPropertyName("dependentDob")]
        public string? DependentDob { get; set; }

        [JsonPropertyName("dependentLivesSameHome")]
        public bool? DependentLivesSameHome { get; set; }

        [JsonPropertyName("dependentDisabled")]
        public bool? DependentDisabled { get; set; }

        [JsonPropertyName("caregivingHoursPerWeek")]
        public decimal? CaregivingHoursPerWeek { get; set; }

        [JsonPropertyName("effectiveBeginDate")]
        public string? EffectiveBeginDate { get; set; }

        [JsonPropertyName("effectiveEndDate")]
        public string? EffectiveEndDate { get; set; }
    }

    public class FosterCareExclusionDTO
    {
        [JsonPropertyName("circumstanceCode")]
        public string? CircumstanceCode { get; set; }

        [JsonPropertyName("receivingIVEFosterCare")]
        public bool? ReceivingIveFosterCare { get; set; }

        [JsonPropertyName("inStateCustody")]
        public bool? InStateCustody { get; set; }

        [JsonPropertyName("effectiveBeginDate")]
        public string? EffectiveBeginDate { get; set; }

        [JsonPropertyName("effectiveEndDate")]
        public string? EffectiveEndDate { get; set; }
    }

    public class FormerFosterCareExclusionDTO
    {
        [JsonPropertyName("circumstanceCode")]
        public string? CircumstanceCode { get; set; }

        [JsonPropertyName("enrolledMedicaidFosterCareAge18")]
        public bool? EnrolledMedicaidFosterCareAge18 { get; set; }

        [JsonPropertyName("effectiveBeginDate")]
        public string? EffectiveBeginDate { get; set; }

        [JsonPropertyName("effectiveEndDate")]
        public string? EffectiveEndDate { get; set; }
    }

    public class IncarcerationExclusionDTO
    {
        [JsonPropertyName("circumstanceCode")]
        public string? CircumstanceCode { get; set; }

        [JsonPropertyName("toa")]
        public string? Toa { get; set; }

        [JsonPropertyName("livingArrangementType")]
        public string? LivingArrangementType { get; set; }

        [JsonPropertyName("incarceratedLast3Months")]
        public bool? IncarceratedLast3Months { get; set; }

        [JsonPropertyName("facilityType")]
        public string? FacilityType { get; set; }

        [JsonPropertyName("effectiveBeginDate")]
        public string? EffectiveBeginDate { get; set; }

        [JsonPropertyName("effectiveEndDate")]
        public string? EffectiveEndDate { get; set; }
    }

    public class MedicarePartAbExclusionDTO
    {
        [JsonPropertyName("coverageType")]
        public string? CoverageType { get; set; }

        [JsonPropertyName("medicareExpenseType")]
        public string? MedicareExpenseType { get; set; }

        [JsonPropertyName("solq")]
        public bool? Solq { get; set; }

        [JsonPropertyName("startDate")]
        public string? StartDate { get; set; }

        [JsonPropertyName("endDate")]
        public string? EndDate { get; set; }
    }

    public class FrailtyExclusionDTO
    {
        [JsonPropertyName("seriousMedicalConditionText")]
        public string? SeriousMedicalConditionText { get; set; }

        [JsonPropertyName("mentalHealthPhysicalLimitationText")]
        public string? MentalHealthPhysicalLimitationText { get; set; }

        [JsonPropertyName("seriousHealthImpact")]
        public bool? SeriousHealthImpact { get; set; }

        [JsonPropertyName("mentalHealth")]
        public bool? MentalHealth { get; set; }

        [JsonPropertyName("physicalDisability")]
        public bool? PhysicalDisability { get; set; }

        [JsonPropertyName("verificationCode")]
        public string? VerificationCode { get; set; }

        [JsonPropertyName("effectiveStartDate")]
        public string? EffectiveStartDate { get; set; }

        [JsonPropertyName("effectiveEndDate")]
        public string? EffectiveEndDate { get; set; }
    }

    // ─── Engagements ──────────────────────────────────────────────────────────────

    public class EngagementsDTO
    {
        [JsonPropertyName("employment")]
        public List<EmploymentEntryDTO>? Employment { get; set; }

        [JsonPropertyName("jobTraining")]
        public List<JobTrainingEntryDTO>? JobTraining { get; set; }

        [JsonPropertyName("education")]
        public List<EducationEntryDTO>? Education { get; set; }

        /// <summary>Truv/payroll-provider verified employer tree — feeds CE_TRUV_EMPLOYER and its children.</summary>
        [JsonPropertyName("electronicallyVerifiedEmployment")]
        public List<TruvEmployerDTO>? ElectronicallyVerifiedEmployment { get; set; }

        /// <summary>Maps to CE_ELECTRONICALLY_VERIFIED_EMPLOYMENT (monthly income/hours summary at the member level).</summary>
        [JsonPropertyName("evIndvIncomeBudget")]
        public List<ElectronicallyVerifiedEmploymentDTO>? EvIndvIncomeBudget { get; set; }

        /// <summary>No corresponding table in TableDefinition.txt — parsed but not persisted.</summary>
        [JsonPropertyName("evEdgIncomeBudget")]
        public object? EvEdgIncomeBudget { get; set; }

        /// <summary>No corresponding table in TableDefinition.txt — parsed but not persisted.</summary>
        [JsonPropertyName("budgetGroupMembers")]
        public object? BudgetGroupMembers { get; set; }

        /// <summary>No corresponding table in TableDefinition.txt — parsed but not persisted.</summary>
        [JsonPropertyName("magiIncomeBudget")]
        public object? MagiIncomeBudget { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("volunteeringCommunityService")]
        public List<VolunteeringEntryDTO>? VolunteeringCommunityService { get; set; }

        [JsonPropertyName("updateSw")]
        public string? UpdateSw { get; set; }
    }

    // ─── Employment (CE_EMPLOYMENT / CE_INCOME_BUDGET) ───────────────────────────

    public class EmploymentEntryDTO
    {
        [JsonPropertyName("employerName")]
        public string? EmployerName { get; set; }

        [JsonPropertyName("employerEin")]
        public string? EmployerEin { get; set; }

        [JsonPropertyName("employmentType")]
        public string? EmploymentType { get; set; }

        [JsonPropertyName("isSeasonalEmployment")]
        public bool? IsSeasonalEmployment { get; set; }

        [JsonPropertyName("seasonalEmploymentType")]
        public string? SeasonalEmploymentType { get; set; }

        [JsonPropertyName("isPriorEmployment")]
        public bool? IsPriorEmployment { get; set; }

        [JsonPropertyName("startDate")]
        public string? StartDate { get; set; }

        [JsonPropertyName("endDate")]
        public string? EndDate { get; set; }

        [JsonPropertyName("isEmploymentActive")]
        public bool? IsEmploymentActive { get; set; }

        [JsonPropertyName("isInkindOrUnPaid")]
        public bool? IsInkindOrUnPaid { get; set; }

        [JsonPropertyName("incomeBudgetDetails")]
        public IncomeBudgetDetailsDTO? IncomeBudgetDetails { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updateSw")]
        public string? UpdateSw { get; set; }
    }

    /// <summary>Field names inferred from TableDefinition.txt — sample payload shows null.</summary>
    public class IncomeBudgetDetailsDTO
    {
        [JsonPropertyName("monthlyIncome")]
        public decimal? MonthlyIncome { get; set; }

        [JsonPropertyName("monthlyHours")]
        public decimal? MonthlyHours { get; set; }

        [JsonPropertyName("incomeMonth")]
        public int? IncomeMonth { get; set; }

        [JsonPropertyName("incomeYear")]
        public int? IncomeYear { get; set; }

        [JsonPropertyName("verificationSource")]
        public string? VerificationSource { get; set; }
    }

    /// <summary>Field names inferred from TableDefinition.txt — sample payload shows null.</summary>
    public class ElectronicallyVerifiedEmploymentDTO
    {
        [JsonPropertyName("verificationSource")]
        public string? VerificationSource { get; set; }

        [JsonPropertyName("monthlyIncome")]
        public decimal? MonthlyIncome { get; set; }

        [JsonPropertyName("monthlyHours")]
        public decimal? MonthlyHours { get; set; }

        [JsonPropertyName("incomeMonth")]
        public int? IncomeMonth { get; set; }

        [JsonPropertyName("incomeYear")]
        public int? IncomeYear { get; set; }
    }

    // ─── Job Training (CE_JOB_TRAINING) ──────────────────────────────────────────

    public class JobTrainingEntryDTO
    {
        [JsonPropertyName("organizationId")]
        public string? OrganizationId { get; set; }

        [JsonPropertyName("organizationName")]
        public string? OrganizationName { get; set; }

        [JsonPropertyName("monthlyHours")]
        public decimal? MonthlyHours { get; set; }

        [JsonPropertyName("startDate")]
        public string? StartDate { get; set; }

        [JsonPropertyName("endDate")]
        public string? EndDate { get; set; }

        [JsonPropertyName("verificationDate")]
        public string? VerificationDate { get; set; }

        [JsonPropertyName("verificationSource")]
        public string? VerificationSource { get; set; }

        [JsonPropertyName("programName")]
        public string? ProgramName { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updateSw")]
        public string? UpdateSw { get; set; }
    }

    // ─── Volunteering (CE_VOLUNTEER_SERVICE) ─────────────────────────────────────

    public class VolunteeringEntryDTO
    {
        [JsonPropertyName("organizationId")]
        public string? OrganizationId { get; set; }

        [JsonPropertyName("organizationName")]
        public string? OrganizationName { get; set; }

        [JsonPropertyName("monthlyHours")]
        public decimal? MonthlyHours { get; set; }

        [JsonPropertyName("startDate")]
        public string? StartDate { get; set; }

        [JsonPropertyName("endDate")]
        public string? EndDate { get; set; }

        [JsonPropertyName("verificationSource")]
        public string? VerificationSource { get; set; }

        [JsonPropertyName("verificationDate")]
        public string? VerificationDate { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updateSw")]
        public string? UpdateSw { get; set; }
    }

    // ─── Education (CE_EDUCATION / CE_NSC_ENROLLMENT / CE_NSC_ENROLLMENT_DETAIL /
    //    CE_COURSE_OF_STUDY) ───────────────────────────────────────────────────────

    public class EducationEntryDTO
    {
        [JsonPropertyName("institutionName")]
        public string? InstitutionName { get; set; }

        [JsonPropertyName("institutionType")]
        public string? InstitutionType { get; set; }

        [JsonPropertyName("verificationDate")]
        public string? VerificationDate { get; set; }

        [JsonPropertyName("verificationSource")]
        public string? VerificationSource { get; set; }

        [JsonPropertyName("enrollmentStatus")]
        public string? EnrollmentStatus { get; set; }

        [JsonPropertyName("monthlyHours")]
        public decimal? MonthlyHours { get; set; }

        [JsonPropertyName("termStartDate")]
        public string? TermStartDate { get; set; }

        [JsonPropertyName("termEndDate")]
        public string? TermEndDate { get; set; }

        [JsonPropertyName("electronicallyVerifiedData")]
        public List<NscEnrollmentDTO>? ElectronicallyVerifiedData { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updateSw")]
        public string? UpdateSw { get; set; }
    }

    public class NscEnrollmentDTO
    {
        [JsonPropertyName("officialSchoolName")]
        public string? OfficialSchoolName { get; set; }

        [JsonPropertyName("schoolCode")]
        public string? SchoolCode { get; set; }

        [JsonPropertyName("branchCode")]
        public string? BranchCode { get; set; }

        [JsonPropertyName("currentEnrollmentStatus")]
        public string? CurrentEnrollmentStatus { get; set; }

        [JsonPropertyName("enrollmentData")]
        public List<NscEnrollmentDetailDTO>? EnrollmentData { get; set; }
    }

    public class NscEnrollmentDetailDTO
    {
        [JsonPropertyName("enrollmentStatus")]
        public string? EnrollmentStatus { get; set; }

        [JsonPropertyName("termBeginDate")]
        public string? TermBeginDate { get; set; }

        [JsonPropertyName("termEndDate")]
        public string? TermEndDate { get; set; }

        [JsonPropertyName("anticipatedGraduationDate")]
        public string? AnticipatedGraduationDate { get; set; }

        [JsonPropertyName("schoolCertifiedOnDate")]
        public string? SchoolCertifiedOnDate { get; set; }

        [JsonPropertyName("majorCoursesOfStudy")]
        public List<CourseOfStudyDTO>? MajorCoursesOfStudy { get; set; }
    }

    public class CourseOfStudyDTO
    {
        [JsonPropertyName("course")]
        public string? Course { get; set; }

        [JsonPropertyName("ncesCIPCode")]
        public string? NcesCipCode { get; set; }
    }

    // ─── Truv Verified Employment (CE_TRUV_EMPLOYER / CE_TRUV_PROVIDER /
    //    CE_EMPLOYMENT_DETAIL / CE_EMPLOYEE_PROFILE / CE_PAY_STATEMENT /
    //    CE_ANNUAL_INCOME) ─────────────────────────────────────────────────────────
    // Null in the sample payload — field names carried over from the prior DTO
    // version (Truv's standard payroll-verification shape) and expanded to cover
    // every CE_PAY_STATEMENT / CE_ANNUAL_INCOME column.

    public class TruvEmployerDTO
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

        [JsonPropertyName("is_suspicious")]
        public bool? IsSuspicious { get; set; }

        [JsonPropertyName("provider")]
        public TruvProviderDTO? Provider { get; set; }

        [JsonPropertyName("employments")]
        public List<TruvEmploymentDetailDTO>? Employments { get; set; }
    }

    public class TruvProviderDTO
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("logo_url")]
        public string? LogoUrl { get; set; }
    }

    public class TruvEmploymentDetailDTO
    {
        [JsonPropertyName("employment_external_id")]
        public string? EmploymentExternalId { get; set; }

        [JsonPropertyName("job_title")]
        public string? JobTitle { get; set; }

        [JsonPropertyName("job_type")]
        public string? JobType { get; set; }

        [JsonPropertyName("start_date")]
        public string? StartDate { get; set; }

        [JsonPropertyName("end_date")]
        public string? EndDate { get; set; }

        [JsonPropertyName("is_active")]
        public bool? IsActive { get; set; }

        [JsonPropertyName("annual_income")]
        public decimal? AnnualIncome { get; set; }

        [JsonPropertyName("income_unit")]
        public string? IncomeUnit { get; set; }

        [JsonPropertyName("pay_rate")]
        public decimal? PayRate { get; set; }

        [JsonPropertyName("pay_frequency")]
        public string? PayFrequency { get; set; }

        [JsonPropertyName("profile")]
        public TruvEmployeeProfileDTO? Profile { get; set; }

        [JsonPropertyName("statements")]
        public List<TruvPayStatementDTO>? Statements { get; set; }

        [JsonPropertyName("annual_income_summary")]
        public List<TruvAnnualIncomeDTO>? AnnualIncomeSummary { get; set; }
    }

    public class TruvEmployeeProfileDTO
    {
        [JsonPropertyName("employee_id")]
        public string? EmployeeId { get; set; }

        [JsonPropertyName("full_name")]
        public string? FullName { get; set; }
    }

    public class TruvPayStatementDTO
    {
        [JsonPropertyName("pay_statement_external_id")]
        public string? PayStatementExternalId { get; set; }

        [JsonPropertyName("pay_date")]
        public string? PayDate { get; set; }

        [JsonPropertyName("net_pay")]
        public decimal? NetPay { get; set; }

        [JsonPropertyName("net_pay_ytd")]
        public decimal? NetPayYtd { get; set; }

        [JsonPropertyName("gross_pay")]
        public decimal? GrossPay { get; set; }

        [JsonPropertyName("gross_pay_ytd")]
        public decimal? GrossPayYtd { get; set; }

        [JsonPropertyName("bonus")]
        public decimal? Bonus { get; set; }

        [JsonPropertyName("commission")]
        public decimal? Commission { get; set; }

        [JsonPropertyName("hours")]
        public decimal? Hours { get; set; }

        [JsonPropertyName("basis_of_pay")]
        public string? BasisOfPay { get; set; }

        [JsonPropertyName("period_start")]
        public string? PeriodStart { get; set; }

        [JsonPropertyName("period_end")]
        public string? PeriodEnd { get; set; }

        [JsonPropertyName("regular")]
        public decimal? Regular { get; set; }

        [JsonPropertyName("regular_ytd")]
        public decimal? RegularYtd { get; set; }

        [JsonPropertyName("other_pay")]
        public decimal? OtherPay { get; set; }

        [JsonPropertyName("other_pay_ytd")]
        public decimal? OtherPayYtd { get; set; }

        [JsonPropertyName("bonus_ytd")]
        public decimal? BonusYtd { get; set; }

        [JsonPropertyName("commission_ytd")]
        public decimal? CommissionYtd { get; set; }

        [JsonPropertyName("overtime")]
        public decimal? Overtime { get; set; }

        [JsonPropertyName("overtime_ytd")]
        public decimal? OvertimeYtd { get; set; }
    }

    public class TruvAnnualIncomeDTO
    {
        [JsonPropertyName("annual_income_external_id")]
        public string? AnnualIncomeExternalId { get; set; }

        [JsonPropertyName("report_year")]
        public int? ReportYear { get; set; }

        [JsonPropertyName("regular")]
        public decimal? Regular { get; set; }

        [JsonPropertyName("bonus")]
        public decimal? Bonus { get; set; }

        [JsonPropertyName("commission")]
        public decimal? Commission { get; set; }

        [JsonPropertyName("overtime")]
        public decimal? Overtime { get; set; }

        [JsonPropertyName("other_pay")]
        public decimal? OtherPay { get; set; }

        [JsonPropertyName("net_pay")]
        public decimal? NetPay { get; set; }

        [JsonPropertyName("gross_pay")]
        public decimal? GrossPay { get; set; }
    }

    // ─── Shared reference types ──────────────────────────────────────────────────

    public class DocumentRefDTO
    {
        [JsonPropertyName("documentType")]
        public string? DocumentType { get; set; }

        [JsonPropertyName("fileName")]
        public string? FileName { get; set; }
    }
}
