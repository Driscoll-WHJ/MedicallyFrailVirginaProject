using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MES_EDWS.Models
{
    // ─── Root Request ────────────────────────────────────────────────────────────
    // Shape rebuilt to match docs/NewPayloadPost.json (CEP-ICD-003, 2026-08 revision)
    // and to feed the HR1_DMAS_POC.MWRP_CE_* tables described in docs/TableDefinition.txt
    // plus docs/SchemaChanges_NewPayload.sql (new tables/columns added for this revision).

    public class CepDWRequestDTO
    {
        [Required]
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

        [Required]
        [JsonPropertyName("requestSequenceNumber")]
        public string RequestSequenceNumber { get; set; } = string.Empty;

        /// <summary>Renamed from "state" to "stateId" in the 2026-08 ICD revision.</summary>
        [Required]
        [JsonPropertyName("stateId")]
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

    public class DocumentUploadDTO
    {
        [JsonPropertyName("documentType")]
        public string? DocumentType { get; set; }

        [JsonPropertyName("documentSubType")]
        public string? DocumentSubtype { get; set; }

        [JsonPropertyName("clientId")]
        public long? ClientId { get; set; }

        [JsonPropertyName("documentId")]
        public long? DocumentId { get; set; }

        [JsonPropertyName("documentUploadDate")]
        public DateTime? DocumentUploadTimestamp { get; set; }

        [JsonPropertyName("documentDeletedDate")]
        public DateTime? DocumentDeletedTimestamp { get; set; }

        [JsonPropertyName("source")]
        public string? SourceSystem { get; set; }

        [JsonPropertyName("status")]
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

        /// <summary>New in the 2026-08 revision (replaces the old "lookbackMonth" field). Persisted to CE_VERIFIED.REVIEW_MONTH_DT.</summary>
        [JsonPropertyName("reviewMonth")]
        public string? ReviewMonth { get; set; }

        /// <summary>Persisted to the new MWRP_CE_RULES_EVALUATION_RESULT table.</summary>
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

        /// <summary>Replaces the old separate "exclusions"/"exceptions" objects in the 2026-08 revision.</summary>
        [JsonPropertyName("exemptions")]
        public ExemptionsDTO? Exemptions { get; set; }

        [JsonPropertyName("engagements")]
        public EngagementsDTO? Engagements { get; set; }

        [JsonPropertyName("mmisEnrolleeID")]
        public long? MmisEnrolleeId { get; set; }
    }

    public class RuleEvaluationResultDTO
    {
        [JsonPropertyName("ruleId")]
        public string? RuleId { get; set; }

        [JsonPropertyName("ruleName")]
        public string? RuleName { get; set; }

        [JsonPropertyName("result")]
        public string? Result { get; set; }

        [JsonPropertyName("activityType")]
        public string? ActivityType { get; set; }
    }

    // ─── Aid Category (CE_AID_CATEGORY) ──────────────────────────────────────────

    public class AidCategoryDTO
    {
        /// <summary>JSON field is named "aidCategory" (not "code") in the 2026-08 revision.</summary>
        [JsonPropertyName("aidCategory")]
        public int? Code { get; set; }

        [JsonPropertyName("startDate")]
        public string? StartDate { get; set; }

        [JsonPropertyName("endDate")]
        public string? EndDate { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updatesw")]
        public string? UpdateSw { get; set; }

        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }

        [JsonPropertyName("applicationSource")]
        public string? ApplicationSource { get; set; }
    }

    // ─── Phone (CE_PHONE) ─────────────────────────────────────────────────────────

    public class PhoneDataDTO
    {
        [JsonPropertyName("phoneType")]
        public string? PhoneType { get; set; }

        [JsonPropertyName("phoneNumber")]
        public string? PhoneNumber { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updatesw")]
        public string? UpdateSw { get; set; }

        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }

        [JsonPropertyName("applicationSource")]
        public string? ApplicationSource { get; set; }
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

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updatesw")]
        public string? UpdateSw { get; set; }

        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }

        [JsonPropertyName("applicationSource")]
        public string? ApplicationSource { get; set; }
    }

    // ─── Exemptions (CE_EXCLUSION_* / CE_VETERAN_DISABILITY) ─────────────────────
    // Replaces the old ExclusionsDTO/ExceptionsDTO split — the 2026-08 payload nests
    // everything under a single "exemptions" object.

    public class ExemptionsDTO
    {
        [JsonPropertyName("pregnancy")]
        public PregnancyExemptionDTO? Pregnancy { get; set; }

        /// <summary>Now an array in the 2026-08 revision (was a single object).</summary>
        [JsonPropertyName("careGiverCircumstance")]
        public List<CaregiverExemptionDTO>? CareGiverCircumstance { get; set; }

        [JsonPropertyName("formerFosterCare")]
        public FormerFosterCareExemptionDTO? FormerFosterCare { get; set; }

        /// <summary>Renamed from "incarceratedCircumstance" to "currentlyIncarcerated".</summary>
        [JsonPropertyName("currentlyIncarcerated")]
        public IncarcerationExemptionDTO? CurrentlyIncarcerated { get; set; }

        /// <summary>Renamed from "medicarePartAB" to "receivingMedicaidPartAB" and is now an array.</summary>
        [JsonPropertyName("receivingMedicaidPartAB")]
        public List<MedicarePartAbExemptionDTO>? ReceivingMedicaidPartAb { get; set; }

        /// <summary>Renamed from "frailty" to "medicallyFrail".</summary>
        [JsonPropertyName("medicallyFrail")]
        public FrailtyExemptionDTO? MedicallyFrail { get; set; }

        /// <summary>New in the 2026-08 revision — persisted to the new MWRP_CE_VETERAN_DISABILITY table.</summary>
        [JsonPropertyName("veteranWith100PercentDisability")]
        public VeteranDisabilityExemptionDTO? VeteranWith100PercentDisability { get; set; }

        /// <summary>Only carries lineage fields in the sample payload — persisted directly on CE_VERIFIED.</summary>
        [JsonPropertyName("circumstances")]
        public LineageDTO? Circumstances { get; set; }
    }

    /// <summary>Shared source/update-tracking fields that appear on most exemption/engagement objects.</summary>
    public class LineageDTO
    {
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updatesw")]
        public string? UpdateSw { get; set; }

        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }

        [JsonPropertyName("applicationSource")]
        public string? ApplicationSource { get; set; }
    }

    public class PregnancyExemptionDTO
    {
        [JsonPropertyName("circumstanceCode")]
        public string? CircumstanceCode { get; set; }

        [JsonPropertyName("expectedPregnancyDueDate")]
        public string? ExpectedDueDate { get; set; }

        [JsonPropertyName("actualPregnancyEndDate")]
        public string? ActualPregnancyEndDate { get; set; }

        [JsonPropertyName("effectiveBeginDate")]
        public string? EffectiveBeginDate { get; set; }

        [JsonPropertyName("effectiveEndDate")]
        public string? EffectiveEndDate { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updatesw")]
        public string? UpdateSw { get; set; }

        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }

        [JsonPropertyName("applicationSource")]
        public string? ApplicationSource { get; set; }
    }

    public class CaregiverExemptionDTO
    {
        [JsonPropertyName("circumstanceCode")]
        public string? CircumstanceCode { get; set; }

        [JsonPropertyName("relationshipWithDependent")]
        public string? RelationshipWithDependent { get; set; }

        [JsonPropertyName("dependentLiveInTheSameHome")]
        public bool? DependentLivesSameHome { get; set; }

        [JsonPropertyName("dependentDOB")]
        public string? DependentDob { get; set; }

        /// <summary>New in the 2026-08 revision — replaces the old scalar "caregivingHoursPerWeek" field.
        /// Persisted to the new MWRP_CE_CAREGIVER_MONTHLY_HOURS child table.</summary>
        [JsonPropertyName("hoursOfCaregivingPerWeekMonth")]
        public List<CaregiverMonthlyHoursDTO>? HoursOfCaregivingPerWeekMonth { get; set; }

        [JsonPropertyName("isDependentDisabled")]
        public bool? IsDependentDisabled { get; set; }

        [JsonPropertyName("effectiveBeginDate")]
        public string? EffectiveBeginDate { get; set; }

        [JsonPropertyName("effectiveEndDate")]
        public string? EffectiveEndDate { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updatesw")]
        public string? UpdateSw { get; set; }

        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }

        [JsonPropertyName("applicationSource")]
        public string? ApplicationSource { get; set; }
    }

    public class CaregiverMonthlyHoursDTO
    {
        [JsonPropertyName("monthAndYear")]
        public string? MonthAndYear { get; set; }

        [JsonPropertyName("monthlyHours")]
        public decimal? MonthlyHours { get; set; }
    }

    public class FormerFosterCareExemptionDTO
    {
        [JsonPropertyName("circumstanceCode")]
        public string? CircumstanceCode { get; set; }

        [JsonPropertyName("enrolledInMedicaidAndFosterCareAtAge18")]
        public bool? EnrolledMedicaidFosterCareAge18 { get; set; }

        /// <summary>New in the 2026-08 revision — persisted to CE_EXCLUSION_FORMER_FOSTER_CARE.AGE_QTY.</summary>
        [JsonPropertyName("age")]
        public int? Age { get; set; }

        [JsonPropertyName("effectiveBeginDate")]
        public string? EffectiveBeginDate { get; set; }

        [JsonPropertyName("effectiveEndDate")]
        public string? EffectiveEndDate { get; set; }

        /// <summary>New in the 2026-08 revision — persisted to CE_EXCLUSION_FORMER_FOSTER_CARE.AID_CATEGORY_WHEN_TURNED18_CD.</summary>
        [JsonPropertyName("aidCategoryWhenTurned18")]
        public int? AidCategoryWhenTurned18 { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updatesw")]
        public string? UpdateSw { get; set; }

        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }

        [JsonPropertyName("applicationSource")]
        public string? ApplicationSource { get; set; }
    }

    /// <summary>Backs the "currentlyIncarcerated" exemption (was "incarceratedCircumstance").</summary>
    public class IncarcerationExemptionDTO
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

        /// <summary>New in the 2026-08 revision — persisted to CE_INCARCERATION.RELEASED_DT.</summary>
        [JsonPropertyName("releasedDate")]
        public string? ReleasedDate { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updatesw")]
        public string? UpdateSw { get; set; }

        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }

        [JsonPropertyName("applicationSource")]
        public string? ApplicationSource { get; set; }
    }

    /// <summary>Item of the "receivingMedicaidPartAB" array (was a single "medicarePartAB" object).</summary>
    public class MedicarePartAbExemptionDTO
    {
        [JsonPropertyName("circumstanceCode")]
        public string? CircumstanceCode { get; set; }

        [JsonPropertyName("typeOfCoverage")]
        public string? CoverageType { get; set; }

        [JsonPropertyName("medicareExpenseType")]
        public string? MedicareExpenseType { get; set; }

        [JsonPropertyName("isSOLQ")]
        public bool? Solq { get; set; }

        [JsonPropertyName("startDate")]
        public string? StartDate { get; set; }

        [JsonPropertyName("endDate")]
        public string? EndDate { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updatesw")]
        public string? UpdateSw { get; set; }

        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }

        [JsonPropertyName("applicationSource")]
        public string? ApplicationSource { get; set; }
    }

    /// <summary>Backs the "medicallyFrail" exemption (was "frailty").</summary>
    public class FrailtyExemptionDTO
    {
        [JsonPropertyName("circumstanceCode")]
        public string? CircumstanceCode { get; set; }

        [JsonPropertyName("seriousMedicalCondition")]
        public string? SeriousMedicalConditionText { get; set; }

        [JsonPropertyName("mentalHealthOrPhysicalLimitation")]
        public string? MentalHealthPhysicalLimitationText { get; set; }

        [JsonPropertyName("seriousHealthProblemImpactsYourLife")]
        public bool? SeriousHealthImpact { get; set; }

        [JsonPropertyName("mentalHealth")]
        public bool? MentalHealth { get; set; }

        [JsonPropertyName("physicalDisability")]
        public bool? PhysicalDisability { get; set; }

        [JsonPropertyName("verification")]
        public string? VerificationCode { get; set; }

        [JsonPropertyName("effectiveStartDate")]
        public string? EffectiveStartDate { get; set; }

        [JsonPropertyName("effectiveEndDate")]
        public string? EffectiveEndDate { get; set; }

        /// <summary>New in the 2026-08 revision — persisted to CE_FRAILTY.DRUG_ALCOHOL_USE_DISORDER_FLG.</summary>
        [JsonPropertyName("drugOrAlcoholUseDisorder")]
        public bool? DrugOrAlcoholUseDisorder { get; set; }

        /// <summary>New in the 2026-08 revision — persisted to CE_FRAILTY.FREQUENT_MEDICAL_CARE_FLG.</summary>
        [JsonPropertyName("frequentMedicalCare")]
        public bool? FrequentMedicalCare { get; set; }

        /// <summary>New in the 2026-08 revision — persisted to CE_FRAILTY.RELIED_ON_MEDICAL_EQUIPMENT_FLG.</summary>
        [JsonPropertyName("reliedOnMedicalEquipment")]
        public bool? ReliedOnMedicalEquipment { get; set; }

        /// <summary>New in the 2026-08 revision — persisted to CE_FRAILTY.BLIND_OR_LIMITED_VISION_FLG.</summary>
        [JsonPropertyName("blindOrHaveLimitedVision")]
        public bool? BlindOrHaveLimitedVision { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updatesw")]
        public string? UpdateSw { get; set; }

        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }

        [JsonPropertyName("applicationSource")]
        public string? ApplicationSource { get; set; }
    }

    /// <summary>New in the 2026-08 revision — persisted to the new MWRP_CE_VETERAN_DISABILITY table.</summary>
    public class VeteranDisabilityExemptionDTO
    {
        [JsonPropertyName("circumstanceCode")]
        public string? CircumstanceCode { get; set; }

        [JsonPropertyName("verificationSource")]
        public string? VerificationSource { get; set; }

        [JsonPropertyName("verificationDate")]
        public string? VerificationDate { get; set; }

        [JsonPropertyName("veteranWith100PercentDisability")]
        public bool? VeteranWith100PercentDisability { get; set; }

        [JsonPropertyName("effectiveBeginDate")]
        public string? EffectiveBeginDate { get; set; }

        [JsonPropertyName("effectiveEndDate")]
        public string? EffectiveEndDate { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updatesw")]
        public string? UpdateSw { get; set; }

        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }

        [JsonPropertyName("applicationSource")]
        public string? ApplicationSource { get; set; }
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

        /// <summary>Simple verified income/hours summary — feeds MWRP_CE_ELECTRONICALLY_VERIFIED_EMPLOYMENT.
        /// (Not to be confused with the richer Truv tree, which now lives at employment[].electronicallyVerifiedData.)</summary>
        [JsonPropertyName("electronicallyVerifiedEmployment")]
        public List<ElectronicallyVerifiedEmploymentDTO>? ElectronicallyVerifiedEmployment { get; set; }

        /// <summary>New shape in the 2026-08 revision — persisted to the new MWRP_CE_INDV_INCOME_BUDGET table.</summary>
        [JsonPropertyName("evIndvIncomeBudget")]
        public List<IndvIncomeBudgetDTO>? EvIndvIncomeBudget { get; set; }

        /// <summary>Persisted to the new MWRP_CE_EDG_INCOME_BUDGET table.</summary>
        [JsonPropertyName("evEdgIncomeBudget")]
        public List<EdgIncomeBudgetDTO>? EvEdgIncomeBudget { get; set; }

        /// <summary>Persisted to the new MWRP_CE_BUDGET_GROUP_MEMBER table.</summary>
        [JsonPropertyName("budgetGroupMembers")]
        public List<BudgetGroupMemberDTO>? BudgetGroupMembers { get; set; }

        /// <summary>Same shape as evEdgIncomeBudget — persisted to the new MWRP_CE_MAGI_INCOME_BUDGET table.</summary>
        [JsonPropertyName("magiIncomeBudget")]
        public List<EdgIncomeBudgetDTO>? MagiIncomeBudget { get; set; }

        [JsonPropertyName("volunteeringCommunityService")]
        public List<VolunteeringEntryDTO>? VolunteeringCommunityService { get; set; }

        /// <summary>New in the 2026-08 revision — persisted to the new MWRP_CE_INKIND_UNPAID_WORK table.</summary>
        [JsonPropertyName("inKindOrUnpaidWork")]
        public List<JobTrainingEntryDTO>? InKindOrUnpaidWork { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

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

        /// <summary>Now an array in the 2026-08 revision (was a single object).</summary>
        [JsonPropertyName("incomeBudgetDetails")]
        public List<IncomeBudgetDetailsDTO>? IncomeBudgetDetails { get; set; }

        /// <summary>New in the 2026-08 revision — the Truv/payroll-provider verified employer tree now
        /// nests here (per employment entry) instead of at the top level of "engagements".</summary>
        [JsonPropertyName("electronicallyVerifiedData")]
        public List<TruvEmployerDTO>? ElectronicallyVerifiedData { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updateSw")]
        public string? UpdateSw { get; set; }

        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }

        [JsonPropertyName("applicationSource")]
        public string? ApplicationSource { get; set; }
    }

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

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updatesw")]
        public string? UpdateSw { get; set; }

        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }

        [JsonPropertyName("applicationSource")]
        public string? ApplicationSource { get; set; }
    }

    /// <summary>Simple verified income/hours summary item — feeds MWRP_CE_ELECTRONICALLY_VERIFIED_EMPLOYMENT.</summary>
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

    /// <summary>engagements.evIndvIncomeBudget[] — persisted to the new MWRP_CE_INDV_INCOME_BUDGET table.</summary>
    public class IndvIncomeBudgetDTO
    {
        [JsonPropertyName("clientId")]
        public long? ClientId { get; set; }

        [JsonPropertyName("ein")]
        public string? Ein { get; set; }

        [JsonPropertyName("budgetBeginDt")]
        public string? BudgetBeginDate { get; set; }

        [JsonPropertyName("budgetEndDt")]
        public string? BudgetEndDate { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("totalBudgetAmount")]
        public decimal? TotalBudgetAmount { get; set; }
    }

    /// <summary>engagements.evEdgIncomeBudget[] and engagements.magiIncomeBudget[] (same shape) — persisted
    /// to the new MWRP_CE_EDG_INCOME_BUDGET / MWRP_CE_MAGI_INCOME_BUDGET tables respectively.</summary>
    public class EdgIncomeBudgetDTO
    {
        [JsonPropertyName("edgNum")]
        public int? EdgNum { get; set; }

        [JsonPropertyName("eligBeginDt")]
        public string? EligBeginDate { get; set; }

        [JsonPropertyName("eligEndDt")]
        public string? EligEndDate { get; set; }

        [JsonPropertyName("totalBudgetAmount")]
        public decimal? TotalBudgetAmount { get; set; }
    }

    /// <summary>engagements.budgetGroupMembers[] — persisted to the new MWRP_CE_BUDGET_GROUP_MEMBER table.</summary>
    public class BudgetGroupMemberDTO
    {
        [JsonPropertyName("edgNum")]
        public int? EdgNum { get; set; }

        [JsonPropertyName("clientId")]
        public long? ClientId { get; set; }

        [JsonPropertyName("EligParfStatusCd")]
        public string? EligParfStatusCd { get; set; }
    }

    // ─── Job Training / In-Kind or Unpaid Work (CE_JOB_TRAINING / CE_INKIND_UNPAID_WORK) ─────

    public class JobTrainingEntryDTO
    {
        /// <summary>Sent as a JSON number in the 2026-08 revision.</summary>
        [JsonPropertyName("organizationId")]
        public long? OrganizationId { get; set; }

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

        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }

        [JsonPropertyName("applicationSource")]
        public string? ApplicationSource { get; set; }
    }

    // ─── Volunteering (CE_VOLUNTEER_SERVICE) ─────────────────────────────────────

    public class VolunteeringEntryDTO
    {
        [JsonPropertyName("organizationID")]
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

        [JsonPropertyName("updatesw")]
        public string? UpdateSw { get; set; }

        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }

        [JsonPropertyName("applicationSource")]
        public string? ApplicationSource { get; set; }
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

        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }

        [JsonPropertyName("applicationSource")]
        public string? ApplicationSource { get; set; }
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
    // Field names updated to the camelCase shape observed nested under
    // employment[].electronicallyVerifiedData in the 2026-08 revision (previously
    // this tree was expected — incorrectly — as a snake_case top-level array).
    // Several numeric fields (income, payRate, pay-statement amounts) arrive as
    // quoted JSON strings; Program.cs enables JsonNumberHandling.AllowReadingFromString
    // so they still bind to decimal/int properties without a custom converter.

    public class TruvEmployerDTO
    {
        [JsonPropertyName("id")]
        public long? Id { get; set; }

        [JsonPropertyName("productType")]
        public string? ProductType { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("dataSource")]
        public string? DataSource { get; set; }

        [JsonPropertyName("companyName")]
        public string? CompanyName { get; set; }

        [JsonPropertyName("isSuspicious")]
        public bool? IsSuspicious { get; set; }

        [JsonPropertyName("startDate")]
        public string? StartDate { get; set; }

        [JsonPropertyName("endDate")]
        public string? EndDate { get; set; }

        [JsonPropertyName("provider")]
        public TruvProviderDTO? Provider { get; set; }

        [JsonPropertyName("employments")]
        public List<TruvEmploymentDetailDTO>? Employments { get; set; }
    }

    public class TruvProviderDTO
    {
        [JsonPropertyName("id")]
        public long? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("logoUrl")]
        public string? LogoUrl { get; set; }
    }

    public class TruvEmploymentDetailDTO
    {
        [JsonPropertyName("id")]
        public long? Id { get; set; }

        [JsonPropertyName("jobTitle")]
        public string? JobTitle { get; set; }

        [JsonPropertyName("jobType")]
        public string? JobType { get; set; }

        [JsonPropertyName("startDate")]
        public string? StartDate { get; set; }

        [JsonPropertyName("endDate")]
        public string? EndDate { get; set; }

        [JsonPropertyName("isActive")]
        public bool? IsActive { get; set; }

        [JsonPropertyName("income")]
        public decimal? Income { get; set; }

        [JsonPropertyName("incomeUnit")]
        public string? IncomeUnit { get; set; }

        [JsonPropertyName("payRate")]
        public decimal? PayRate { get; set; }

        [JsonPropertyName("payFrequency")]
        public string? PayFrequency { get; set; }

        [JsonPropertyName("profile")]
        public TruvEmployeeProfileDTO? Profile { get; set; }

        /// <summary>JSON key is singular ("statement") in the 2026-08 revision.</summary>
        [JsonPropertyName("statement")]
        public List<TruvPayStatementDTO>? Statement { get; set; }

        [JsonPropertyName("annualIncomeSummary")]
        public List<TruvAnnualIncomeDTO>? AnnualIncomeSummary { get; set; }
    }

    public class TruvEmployeeProfileDTO
    {
        [JsonPropertyName("employeeId")]
        public string? EmployeeId { get; set; }

        [JsonPropertyName("fullName")]
        public string? FullName { get; set; }

        [JsonPropertyName("logoUrl")]
        public string? LogoUrl { get; set; }
    }

    public class TruvPayStatementDTO
    {
        [JsonPropertyName("id")]
        public long? Id { get; set; }

        [JsonPropertyName("payDate")]
        public string? PayDate { get; set; }

        [JsonPropertyName("netPay")]
        public decimal? NetPay { get; set; }

        [JsonPropertyName("netPayYtd")]
        public decimal? NetPayYtd { get; set; }

        [JsonPropertyName("grossPay")]
        public decimal? GrossPay { get; set; }

        [JsonPropertyName("grossPayYtd")]
        public decimal? GrossPayYtd { get; set; }

        [JsonPropertyName("bonus")]
        public decimal? Bonus { get; set; }

        [JsonPropertyName("commission")]
        public decimal? Commission { get; set; }

        [JsonPropertyName("hours")]
        public decimal? Hours { get; set; }

        [JsonPropertyName("basisOfPay")]
        public string? BasisOfPay { get; set; }

        [JsonPropertyName("periodStart")]
        public string? PeriodStart { get; set; }

        [JsonPropertyName("periodEnd")]
        public string? PeriodEnd { get; set; }

        [JsonPropertyName("regular")]
        public decimal? Regular { get; set; }

        [JsonPropertyName("regularYtd")]
        public decimal? RegularYtd { get; set; }

        [JsonPropertyName("otherPay")]
        public decimal? OtherPay { get; set; }

        [JsonPropertyName("otherPayYtd")]
        public decimal? OtherPayYtd { get; set; }

        [JsonPropertyName("bonusYtd")]
        public decimal? BonusYtd { get; set; }

        [JsonPropertyName("commissionYtd")]
        public decimal? CommissionYtd { get; set; }

        [JsonPropertyName("overTime")]
        public decimal? OverTime { get; set; }

        [JsonPropertyName("overTimeYtd")]
        public decimal? OverTimeYtd { get; set; }
    }

    public class TruvAnnualIncomeDTO
    {
        [JsonPropertyName("id")]
        public long? Id { get; set; }

        [JsonPropertyName("year")]
        public int? Year { get; set; }

        [JsonPropertyName("regular")]
        public decimal? Regular { get; set; }

        [JsonPropertyName("bonus")]
        public decimal? Bonus { get; set; }

        [JsonPropertyName("commission")]
        public decimal? Commission { get; set; }

        [JsonPropertyName("overTime")]
        public decimal? OverTime { get; set; }

        [JsonPropertyName("otherPay")]
        public decimal? OtherPay { get; set; }

        [JsonPropertyName("netPay")]
        public decimal? NetPay { get; set; }

        [JsonPropertyName("grossPay")]
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
