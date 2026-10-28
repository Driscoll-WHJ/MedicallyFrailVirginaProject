using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MES_EDWS.Models
{
    // ─── Root Request ────────────────────────────────────────────────────────────
    // Shape rebuilt to match docs/SamplePayload.json (the authoritative CEP-ICD-003
    // payload sample as of 2026-09) and to feed the HR1_DMAS_POC.MWRP_CE_* tables
    // described in docs/Table_v17.txt.

    /// <summary>
    /// Accepts a JSON value that is sometimes sent as a quoted string and sometimes as a raw
    /// number (e.g. "ein": 541234567 or "ncesCIPCode": 110701) and always exposes it as a
    /// string, matching the VARCHAR columns these values are persisted to.
    /// </summary>
    public class FlexibleStringConverter : JsonConverter<string?>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.TokenType switch
            {
                JsonTokenType.String => reader.GetString(),
                JsonTokenType.Number => reader.TryGetInt64(out var l) ? l.ToString() : reader.GetDouble().ToString(),
                JsonTokenType.Null => null,
                _ => throw new JsonException($"Cannot convert token type {reader.TokenType} to string.")
            };
        }

        public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
        {
            if (value == null) writer.WriteNullValue();
            else writer.WriteStringValue(value);
        }
    }

    public class CepDWRequestDTO
    {
        /// <summary>
        /// Raw string sent by CEP (e.g. "2026-10-06T14:30:00-04:00"). MWRP_CE_REQUEST.REQUEST_TIMESTAMP
        /// is VARCHAR(100) in v1.7.4, so it is stored exactly as sent; <see cref="TryGetRequestTimestamp"/>
        /// only checks that it is ISO 8601.
        /// </summary>
        [Required]
        [JsonPropertyName("requestTimestamp")]
        public string Timestamp { get; set; } = string.Empty;

        public bool TryGetRequestTimestamp(out DateTimeOffset value) =>
            DateTimeOffset.TryParse(Timestamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);

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

        [Required]
        [JsonPropertyName("caseNumber")]
        public string? CaseNumber { get; set; }

        [Required]
        [JsonPropertyName("sendEmailToCustomer")]
        public bool? SendEmailToCustomer { get; set; }

        [Required]
        [JsonPropertyName("preferredCommunicationLanguage")]
        public string? PreferredCommunicationLanguage { get; set; }

        /// <summary>Renamed from "documentsUploadedInMWRP" to "documentsUploadedIn" in the v1.7.3 payload.</summary>
        [JsonPropertyName("documentsUploadedIn")]
        public List<DocumentUploadDTO>? DocumentsUploadedIn { get; set; }

        /// <summary>
        /// Pre-v1.7.3 name of <see cref="DocumentsUploadedIn"/>, still sent by docs/SamplePayload.json.
        /// Without it those documents bind to nothing and MWRP_CE_DOCUMENT stays empty.
        /// </summary>
        [JsonPropertyName("documentsUploadedInMWRP")]
        public List<DocumentUploadDTO>? DocumentsUploadedInMwrp { get; set; }

        /// <summary>Added to the request body in v1.7.3 (e.g. Paper, CommonHelp, RDE). Persisted to MWRP_CE_REQUEST.APPLICATION_SOURCE.</summary>
        [JsonPropertyName("applicationSource")]
        public string? ApplicationSource { get; set; }

        /// <summary>
        /// docs/SamplePayload.json sends "ceVerified" as an ARRAY (one entry per client on the
        /// case), not a single object as originally modeled.
        /// </summary>
        [Required]
        [JsonPropertyName("ceVerified")]
        public List<CeVerifiedDTO> CeVerified { get; set; } = new();
    }

    // ─── Documents (CE_DOCUMENT) ─────────────────────────────────────────────────

    public class DocumentUploadDTO
    {
        [Required]
        [JsonPropertyName("documentType")]
        public string? DocumentType { get; set; }

        [Required]
        [JsonPropertyName("documentSubType")]
        public string? DocumentSubtype { get; set; }

        /// <summary>Optional since v1.7.4. Sent as a quoted string; persisted to the VARCHAR CLIENT_ID column.</summary>
        [JsonPropertyName("clientId")]
        [JsonConverter(typeof(FlexibleStringConverter))]
        public string? ClientId { get; set; }

        /// <summary>Sent as a quoted string in the v1.7.3 payload; DOCUMENT_ID is VARCHAR(50) in v1.7.2.</summary>
        [Required]
        [JsonPropertyName("documentId")]
        [JsonConverter(typeof(FlexibleStringConverter))]
        public string? DocumentId { get; set; }

        [Required]
        [JsonPropertyName("documentUploadDate")]
        public DateTime? DocumentUploadTimestamp { get; set; }

        [JsonPropertyName("documentDeletedDate")]
        public DateTime? DocumentDeletedTimestamp { get; set; }

        [Required]
        [JsonPropertyName("source")]
        public string? SourceSystem { get; set; }

        [Required]
        [JsonPropertyName("status")]
        public string? DocumentStatus { get; set; }
    }

    // ─── CE Verification (CE_VERIFIED) ───────────────────────────────────────────

    public class CeVerifiedDTO : IValidatableObject
    {
        /// <summary>ICD: applicationDate and renewalInitiatedDate are conditionally required — one of them must be sent.</summary>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(ApplicationDate) && string.IsNullOrWhiteSpace(RenewalInitiatedDate))
                yield return new ValidationResult(
                    "Either applicationDate or renewalInitiatedDate is required.",
                    new[] { nameof(ApplicationDate), nameof(RenewalInitiatedDate) });
        }

        /// <summary>Renamed from "clientIdentificationNumber" to "clientId" in docs/SamplePayload.json.</summary>
        [Required]
        [JsonPropertyName("clientId")]
        public string? ClientIdentificationNumber { get; set; }

        [Required]
        [JsonPropertyName("firstName")]
        public string? FirstName { get; set; }

        [Required]
        [JsonPropertyName("lastName")]
        public string? LastName { get; set; }

        [JsonPropertyName("middleName")]
        public string? MiddleName { get; set; }

        [JsonPropertyName("suffix")]
        public string? Suffix { get; set; }

        [JsonPropertyName("gender")]
        public string? Gender { get; set; }

        [Required]
        [JsonPropertyName("dateOfBirth")]
        public string? DateOfBirth { get; set; }

        [JsonPropertyName("socialSecurityNumber")]
        public string? SocialSecurityNumber { get; set; }

        [Required]
        [JsonPropertyName("mwrStatus")]
        public string? MwrStatus { get; set; }

        /// <summary>Renamed from "mwrStartDate" to "mwrStatusStartDate" in docs/SamplePayload.json.</summary>
        [JsonPropertyName("mwrStatusStartDate")]
        public string? MwrStartDate { get; set; }

        /// <summary>Renamed from "mwrEndDate" to "mwrStatusEndDate" in docs/SamplePayload.json.</summary>
        [JsonPropertyName("mwrStatusEndDate")]
        public string? MwrEndDate { get; set; }

        /// <summary>New in the 2026-08 revision (replaces the old "lookbackMonth" field). Persisted to CE_VERIFIED.REVIEW_MONTH_DT.</summary>
        [JsonPropertyName("reviewMonth")]
        public string? ReviewMonth { get; set; }

        /// <summary>Persisted to the new MWRP_CE_RULES_EVALUATION_RESULT table.</summary>
        [Required]
        [JsonPropertyName("rulesEvaluationResults")]
        public List<RuleEvaluationResultDTO>? RulesEvaluationResults { get; set; }

        /// <summary>No corresponding column in TableDefinition.txt — parsed but not persisted.</summary>
        [JsonPropertyName("exemptOrExceptionOrExclusionPendingSw")]
        public string? ExemptOrExceptionOrExclusionPendingSw { get; set; }

        [JsonPropertyName("previousEligibilityAuthorizationDate")]
        public string? PreviousEligibilityAuthorizationDate { get; set; }

        /// <summary>New in the v17 update — persisted to CE_VERIFIED.LAST_APPROVED_PERIOD_BEGIN_DT.</summary>
        [JsonPropertyName("lastApprovedPeriodBeginDate")]
        public string? LastApprovedPeriodBeginDate { get; set; }

        [Required]
        [JsonPropertyName("currentEligibilityBeginDate")]
        public string? CurrentEligibilityBeginDate { get; set; }

        [JsonPropertyName("applicationChangeSubmissionDate")]
        public string? ApplicationChangeSubmissionDate { get; set; }

        [JsonPropertyName("renewalInitiatedDate")]
        public string? RenewalInitiatedDate { get; set; }

        /// <summary>New in the v17 update — persisted to CE_VERIFIED.CHANGE_DT.</summary>
        [JsonPropertyName("changeDate")]
        public string? ChangeDate { get; set; }

        /// <summary>New in the v17 update — persisted to CE_VERIFIED.APPLICATION_DT.</summary>
        [JsonPropertyName("applicationDate")]
        public string? ApplicationDate { get; set; }

        /// <summary>Replaces "caseAction" in v1.7.4 (RT_INDIVIDUALCASEACTION). Persisted to CE_VERIFIED.CASE_ACTION_CD.</summary>
        [Required]
        [JsonPropertyName("individualCaseAction")]
        public string? IndividualCaseAction { get; set; }

        /// <summary>Pre-v1.7.4 name of <see cref="IndividualCaseAction"/>, still sent by the v1.7.4 sample payload.</summary>
        [JsonPropertyName("caseAction")]
        public string? CaseActionLegacy { set => IndividualCaseAction ??= value; }

        [JsonPropertyName("aidCategory")]
        public List<AidCategoryDTO>? AidCategory { get; set; }

        [JsonPropertyName("dueDate")]
        public string? DueDate { get; set; }

        [JsonPropertyName("driverLicenseNumber")]
        public string? DriverLicenseNumber { get; set; }

        [JsonPropertyName("driverLicenseState")]
        public string? DriverLicenseState { get; set; }

        [JsonPropertyName("passportNumber")]
        public string? PassportNumber { get; set; }

        [JsonPropertyName("passportCountry")]
        public string? PassportCountry { get; set; }

        [JsonPropertyName("nationality")]
        public string? Nationality { get; set; }

        [JsonPropertyName("maritalStatus")]
        public string? MaritalStatus { get; set; }

        [JsonPropertyName("emailVerifiedFlag")]
        public bool? EmailVerifiedFlag { get; set; }

        [Required]
        [JsonPropertyName("primaryModeOfCommunication")]
        public string? PrimaryModeOfCommunication { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("alternateEmail")]
        public string? AlternateEmail { get; set; }

        /// <summary>New in docs/SamplePayload.json — persisted to CE_VERIFIED.VERIFICATION_TYPE.</summary>
        [Required]
        [JsonPropertyName("verificationType")]
        public string? VerificationType { get; set; }

        [JsonPropertyName("phoneData")]
        public List<PhoneDataDTO>? PhoneData { get; set; }

        [Required]
        [JsonPropertyName("addresses")]
        public List<AddressDTO>? Addresses { get; set; }

        [Required]
        [JsonPropertyName("headOfHousehold")]
        public bool? HeadOfHousehold { get; set; }

        [Required]
        [JsonPropertyName("requiresCEEvaluation")]
        public bool? RequiresCeEvaluation { get; set; }

        /// <summary>New in docs/SamplePayload.json — persisted to the MWRP_CE_RELATIONSHIP_DETAILS table.</summary>
        [JsonPropertyName("relationshipDetails")]
        public List<RelationshipDetailDTO>? RelationshipDetails { get; set; }

        [Required]
        [JsonPropertyName("relationshipWithHOH")]
        public string? RelationshipWithHoh { get; set; }

        /// <summary>Replaces the old separate "exclusions"/"exceptions" objects in the 2026-08 revision.</summary>
        [JsonPropertyName("exemptions")]
        public ExemptionsDTO? Exemptions { get; set; }

        [JsonPropertyName("engagements")]
        public EngagementsDTO? Engagements { get; set; }

        /// <summary>ICD type is String; persisted to the VARCHAR(15) MMIS_ENROLLEE_ID column.</summary>
        [JsonPropertyName("mmisEnrolleeID")]
        [JsonConverter(typeof(FlexibleStringConverter))]
        public string? MmisEnrolleeId { get; set; }

        /// <summary>EDWS's earlier medically-frail response, echoed back by CEP. Persisted to MWRP_CE_EDWS_FRAILTY.</summary>
        [JsonPropertyName("edwsMedicalFrailty")]
        public EdwsFrailtyDTO? EdwsMedicalFrailty { get; set; }
    }

    /// <summary>ceVerified[].edwsMedicalFrailty (ICD EDWSFrailtyDTO).</summary>
    public class EdwsFrailtyDTO
    {
        [Required]
        [JsonPropertyName("requestId")]
        public string? RequestId { get; set; }

        [Required]
        [JsonPropertyName("medicallyFrail")]
        public bool? MedicallyFrail { get; set; }

        [JsonPropertyName("circumstanceStartDate")]
        public string? CircumstanceStartDate { get; set; }

        [JsonPropertyName("circumstanceEndDate")]
        public string? CircumstanceEndDate { get; set; }
    }

    /// <summary>ceVerified[].relationshipDetails[] — persisted to the new MWRP_CE_RELATIONSHIP_DETAILS table.</summary>
    public class RelationshipDetailDTO
    {
        /// <summary>ICD type is Number; persisted to the VARCHAR MWRP_CE_RELATIONSHIP_DETAILS.CLIENT_IDENTIFICATION_NBR.</summary>
        [Required]
        [JsonPropertyName("clientId")]
        [JsonConverter(typeof(FlexibleStringConverter))]
        public string? ClientId { get; set; }

        [Required]
        [JsonPropertyName("reverseClientId")]
        public long? ReverseClientId { get; set; }

        [Required]
        [JsonPropertyName("relationType")]
        public string? RelationType { get; set; }

        [Required]
        [JsonPropertyName("effectiveBeginDate")]
        public string? EffectiveBeginDate { get; set; }

        [JsonPropertyName("effectiveEndDate")]
        public string? EffectiveEndDate { get; set; }

        [JsonPropertyName("updateSw")]
        public string? UpdateSw { get; set; }

        [Required]
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [Required]
        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }
    }

    public class RuleEvaluationResultDTO
    {
        [Required]
        [JsonPropertyName("ruleId")]
        public string? RuleId { get; set; }

        [Required]
        [JsonPropertyName("ruleName")]
        public string? RuleName { get; set; }

        [Required]
        [JsonPropertyName("result")]
        public string? Result { get; set; }

        [Required]
        [JsonPropertyName("activityType")]
        public string? ActivityType { get; set; }
    }

    // ─── Aid Category (CE_AID_CATEGORY) ──────────────────────────────────────────

    public class AidCategoryDTO
    {
        /// <summary>JSON field is named "aidCategory" (not "code") in the 2026-08 revision. Sent as a
        /// quoted string (e.g. "102") and persisted as-is to the VARCHAR(20) AID_CATEGORY column.</summary>
        [Required]
        [JsonPropertyName("aidCategory")]
        [JsonConverter(typeof(FlexibleStringConverter))]
        public string? Code { get; set; }

        [Required]
        [JsonPropertyName("startDate")]
        public string? StartDate { get; set; }

        [JsonPropertyName("endDate")]
        public string? EndDate { get; set; }

        [Required]
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updatesw")]
        public string? UpdateSw { get; set; }

        [Required]
        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }
    }

    // ─── Phone (CE_PHONE) ─────────────────────────────────────────────────────────

    public class PhoneDataDTO
    {
        [Required]
        [JsonPropertyName("phoneType")]
        public string? PhoneType { get; set; }

        [Required]
        [JsonPropertyName("phoneNumber")]
        public string? PhoneNumber { get; set; }

        [Required]
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updatesw")]
        public string? UpdateSw { get; set; }

        [Required]
        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }
    }

    // ─── Address (CE_ADDRESS) ─────────────────────────────────────────────────────

    public class AddressDTO
    {
        [Required]
        [JsonPropertyName("addressType")]
        public string? AddressType { get; set; }

        [Required]
        [JsonPropertyName("addressLine1")]
        public string? AddressLine1 { get; set; }

        [JsonPropertyName("addressLine2")]
        public string? AddressLine2 { get; set; }

        [JsonPropertyName("county")]
        public string? County { get; set; }

        [Required]
        [JsonPropertyName("city")]
        public string? City { get; set; }

        [Required]
        [JsonPropertyName("zipCode5")]
        public string? ZipCode5 { get; set; }

        [JsonPropertyName("zipCode4")]
        public string? ZipCode4 { get; set; }

        [Required]
        [JsonPropertyName("state")]
        public string? State { get; set; }

        /// <summary>Sent as a quoted string (e.g. "760"); FIPS_CODE is VARCHAR(20), so it is persisted as-is.</summary>
        [JsonPropertyName("fipsCode")]
        [JsonConverter(typeof(FlexibleStringConverter))]
        public string? FipsCode { get; set; }

        [Required]
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updatesw")]
        public string? UpdateSw { get; set; }

        [Required]
        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }
    }

    // ─── Exemptions (CE_EXCLUSION_* / CE_VETERAN_DISABILITY) ─────────────────────
    // Replaces the old ExclusionsDTO/ExceptionsDTO split — the 2026-08 payload nests
    // everything under a single "exemptions" object.

    public class ExemptionsDTO
    {
        [JsonPropertyName("pregnancy")]
        public PregnancyCircumstanceDTO? Pregnancy { get; set; }

        /// <summary>New in docs/SamplePayload.json — persisted to the new MWRP_CE_AMERICAN_INDIAN_ALASKAN_NATIVE table.</summary>
        [JsonPropertyName("americanIndianAlaskanNativeCircumstance")]
        public AmericanIndianAlaskanNativeDTO? AmericanIndianAlaskanNativeCircumstance { get; set; }

        /// <summary>Now an array in the 2026-08 revision (was a single object).</summary>
        [JsonPropertyName("careGiverCircumstance")]
        public List<CaregiverExemptionDTO>? CareGiverCircumstance { get; set; }

        /// <summary>New in docs/SamplePayload.json — persisted to the new MWRP_CE_FOSTER_CARE table.</summary>
        [JsonPropertyName("fosterCare")]
        public FosterCareExemptionDTO? FosterCare { get; set; }

        [JsonPropertyName("formerFosterCare")]
        public FormerFosterCareExemptionDTO? FormerFosterCare { get; set; }

        /// <summary>Renamed from "currentlyIncarcerated" in v1.7.4.</summary>
        [JsonPropertyName("incarceratedCircumstance")]
        public List<IncarcerationExemptionDTO>? IncarceratedCircumstance { get; set; }

        /// <summary>Pre-v1.7.4 name still sent by the v1.7.3 QA payloads.</summary>
        [JsonPropertyName("currentlyIncarcerated")]
        public List<IncarcerationExemptionDTO>? CurrentlyIncarceratedLegacy { set => IncarceratedCircumstance ??= value; }

        /// <summary>New in docs/SamplePayload.json — persisted to the new MWRP_CE_PRIOR_INCARCERATION_DETAIL table.</summary>
        [JsonPropertyName("priorIncarcerationDetails")]
        public List<PriorIncarcerationDetailDTO>? PriorIncarcerationDetails { get; set; }

        [JsonPropertyName("veteranWith100percentDisability")]
        public VeteranDisabilityExemptionDTO? VeteranWith100PercentDisability { get; set; }

        /// <summary>Renamed from "medicarePartAB" to "receivingMedicaidPartAB" and is now an array.</summary>
        [JsonPropertyName("receivingMedicarePartAB")]
        public List<MedicarePartAbExemptionDTO>? ReceivingMedicaidPartAb { get; set; }

        /// <summary>New in docs/SamplePayload.json — persisted to the new MWRP_CE_MEDICARE_PART_AB_TPL table.</summary>
        [JsonPropertyName("receivingMedicarePartABTPL")]
        public List<MedicarePartAbTplExemptionDTO>? ReceivingMedicarePartAbTpl { get; set; }

        /// <summary>Renamed from "frailty" to "medicallyFrail" and is now an array in docs/SamplePayload.json.</summary>
        [JsonPropertyName("medicallyFrail")]
        public List<FrailtyExemptionDTO>? MedicallyFrail { get; set; }

        /// <summary>New in v1.7.4 (replaces medicallyFrail[].cBCTypeDetails) — persisted to MWRP_CE_CBC_TYPE_DETAILS.</summary>
        [JsonPropertyName("indvCBCTypeDetails")]
        public List<IndvCbcTypeDetailDTO>? IndvCbcTypeDetails { get; set; }

        /// <summary>Spelling used in the v1.7.4 ICD ExemptionsDTO table.</summary>
        [JsonPropertyName("indvCcBCTypeDetails")]
        public List<IndvCbcTypeDetailDTO>? IndvCcbcTypeDetailsMisspelled { set => IndvCbcTypeDetails ??= value; }

        /// <summary>New in v1.7.4 (replaces the FrailtyDTO SOLQ flags) — persisted to MWRP_CE_SOLQ_TITLE_DISABLED.</summary>
        [JsonPropertyName("solqTitleDisability")]
        public List<SolqTitleDisabledDTO>? SolqTitleDisability { get; set; }

        /// <summary>New in v1.7.4 — persisted to MWRP_CE_LTSS_SCREENING.</summary>
        [JsonPropertyName("lTSSScreeningDetails")]
        public List<LtssScreeningDTO>? LtssScreeningDetails { get; set; }

        /// <summary>New in docs/SamplePayload.json — persisted to the new MWRP_CE_MEDICAL_EXEMPTIONS /
        /// MWRP_CE_CARE_RECEIVED_DETAILS tables.</summary>
        [JsonPropertyName("medicalExemptions")]
        public List<MedicalExemptionDTO>? MedicalExemptions { get; set; }

        /// <summary>New in docs/SamplePayload.json — persisted to the new MWRP_CE_TRAVELING_OUTSIDE /
        /// MWRP_CE_CARE_RECEIVED_DETAILS tables.</summary>
        [JsonPropertyName("travelingOutside")]
        public List<TravelingOutsideDTO>? TravelingOutside { get; set; }

        /// <summary>New in docs/SamplePayload.json — persisted to the new
        /// MWRP_CE_SUBSTANCE_USE_DISORDER_TREATMENT / MWRP_CE_TREATMENT_DETAILS tables.</summary>
        [JsonPropertyName("substanceUseDisorderTreatment")]
        public List<SubstanceUseDisorderTreatmentDTO>? SubstanceUseDisorderTreatment { get; set; }

        /// <summary>New in docs/SamplePayload.json — persisted to the new MWRP_CE_DISABILITY_DETAILS table.</summary>
        [JsonPropertyName("disabilityDetails")]
        public List<DisabilityDetailDTO>? DisabilityDetails { get; set; }

        /// <summary>New in docs/SamplePayload.json — persisted to the new MWRP_CE_LIVING_ARRANGEMENT_DETAILS table.</summary>
        [Required]
        [JsonPropertyName("livingArrangementDetails")]
        public List<LivingArrangementDetailDTO>? LivingArrangementDetails { get; set; }

        /// <summary>New in docs/SamplePayload.json — persisted to the new MWRP_CE_CIRCUMSTANCE /
        /// MWRP_CE_CIRCUMSTANCE_DETAIL tables.</summary>
        [JsonPropertyName("circumstanceForExemption")]
        public List<CircumstanceDTO>? CircumstanceForExemption { get; set; }

        /// <summary>Now a rich array (was previously assumed to only carry lineage fields) — persisted to
        /// the same MWRP_CE_CIRCUMSTANCE / MWRP_CE_CIRCUMSTANCE_DETAIL tables as "circumstanceForExemption".
        /// Optional since v1.7.4.</summary>
        [JsonPropertyName("circumstances")]
        public List<CircumstanceDTO>? Circumstances { get; set; }
    }

    /// <summary>exemptions.americanIndianAlaskanNativeCircumstance — persisted to the new
    /// MWRP_CE_AMERICAN_INDIAN_ALASKAN_NATIVE table.</summary>
    public class AmericanIndianAlaskanNativeDTO
    {
        [Required]
        [JsonPropertyName("circumstanceCode")]
        public string? CircumstanceCode { get; set; }

        [Required]
        [JsonPropertyName("americanIndianAlaskanNative")]
        public bool? AmericanIndianAlaskanNative { get; set; }

        [JsonPropertyName("fedRecognizedTribe")]
        public bool? FedRecognizedTribe { get; set; }

        [JsonPropertyName("tribeName")]
        public string? TribeName { get; set; }

        [JsonPropertyName("effectiveBeginDate")]
        public string? EffectiveBeginDate { get; set; }

        [JsonPropertyName("effectiveEndDate")]
        public string? EffectiveEndDate { get; set; }

        [Required]
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updateSw")]
        public string? UpdateSw { get; set; }

        [Required]
        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }
    }

    /// <summary>exemptions.fosterCare — persisted to the new MWRP_CE_FOSTER_CARE table.</summary>
    public class FosterCareExemptionDTO
    {
        [Required]
        [JsonPropertyName("circumstanceCode")]
        public string? CircumstanceCode { get; set; }

        [Required]
        [JsonPropertyName("receivingIVEFosterCare")]
        public bool? ReceivingIveFosterCare { get; set; }

        [Required]
        [JsonPropertyName("inStateCustody")]
        public bool? InStateCustody { get; set; }

        [JsonPropertyName("effectiveBeginDate")]
        public string? EffectiveBeginDate { get; set; }

        [JsonPropertyName("effectiveEndDate")]
        public string? EffectiveEndDate { get; set; }

        [Required]
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updateSw")]
        public string? UpdateSw { get; set; }

        [Required]
        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }
    }

    /// <summary>exemptions.priorIncarcerationDetails[] — persisted to the new
    /// MWRP_CE_PRIOR_INCARCERATION_DETAIL table.</summary>
    public class PriorIncarcerationDetailDTO
    {
        [Required]
        [JsonPropertyName("wasThereIncarcerationInLast4Months")]
        public string? WasIncarceratedLast4Months { get; set; }

        [JsonPropertyName("priorIncarcerationEndDate")]
        public string? PriorIncarcerationEndDate { get; set; }

        [Required]
        [JsonPropertyName("priorIncarcerationType")]
        public string? PriorIncarcerationType { get; set; }
    }

    /// <summary>exemptions.receivingMedicarePartABTPL[] — persisted to the new
    /// MWRP_CE_MEDICARE_PART_AB_TPL table.</summary>
    public class MedicarePartAbTplExemptionDTO
    {
        [Required]
        [JsonPropertyName("typeOfCoverage")]
        public string? CoverageType { get; set; }

        [Required]
        [JsonPropertyName("overAllTypeOfCoverage")]
        public string? OverallTypeOfCoverage { get; set; }

        [JsonPropertyName("isSOLQ")]
        public bool? Solq { get; set; }

        [Required]
        [JsonPropertyName("startDate")]
        public string? StartDate { get; set; }

        [JsonPropertyName("endDate")]
        public string? EndDate { get; set; }

        [Required]
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updateSw")]
        public string? UpdateSw { get; set; }

        [Required]
        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }
    }

    /// <summary>Shared shape for exemptions.medicalExemptions[].careReceivedDetails[] and
    /// exemptions.travelingOutside[].careReceivedDetails[] — persisted to the
    /// MWRP_CE_CARE_RECEIVED_DETAILS table.</summary>
    public class CareReceivedDetailDTO
    {
        /// <summary>ICD v1.7.3 and the QA payloads send "typeOfCareCode".</summary>
        [Required]
        [JsonPropertyName("typeOfCareCode")]
        public string? TypeOfCare { get; set; }

        /// <summary>Older name still sent by docs/SamplePayload.json.</summary>
        [JsonPropertyName("typeOfCare")]
        public string? TypeOfCareLegacy { set => TypeOfCare ??= value; }

        /// <summary>Persisted to MWRP_CE_CARE_RECEIVED_DETAILS.TYPE_OF_CARE_DESC.</summary>
        [JsonPropertyName("typeOfCareDescription")]
        public string? TypeOfCareDescription { get; set; }

        [Required]
        [JsonPropertyName("startDate")]
        public string? StartDate { get; set; }

        [JsonPropertyName("endDate")]
        public string? EndDate { get; set; }

        [Required]
        [JsonPropertyName("providerName")]
        public string? ProviderName { get; set; }

        /// <summary>Persisted to MWRP_CE_CARE_RECEIVED_DETAILS.CARE_PROVIDER_ADDRESS (VARCHAR(200)).</summary>
        [JsonPropertyName("providerAddress")]
        public string? ProviderAddress { get; set; }
    }

    /// <summary>exemptions.medicalExemptions[] — persisted to the new MWRP_CE_MEDICAL_EXEMPTIONS /
    /// MWRP_CE_CARE_RECEIVED_DETAILS tables.</summary>
    public class MedicalExemptionDTO
    {
        [JsonPropertyName("thisMonthOrLastMonthReceivedHospitalCare")]
        public bool? ReceivedHospitalCare { get; set; }

        [Required]
        [JsonPropertyName("effectiveBeginDate")]
        public string? EffectiveBeginDate { get; set; }

        [JsonPropertyName("effectiveEndDate")]
        public string? EffectiveEndDate { get; set; }

        [JsonPropertyName("verificationSource")]
        public string? VerificationSource { get; set; }

        [JsonPropertyName("verificationDate")]
        public string? VerificationDate { get; set; }

        [JsonPropertyName("careReceivedDetails")]
        public List<CareReceivedDetailDTO>? CareReceivedDetails { get; set; }

        [Required]
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updateSw")]
        public string? UpdateSw { get; set; }

        [Required]
        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }
    }

    /// <summary>exemptions.travelingOutside[] — persisted to the new MWRP_CE_TRAVELING_OUTSIDE /
    /// MWRP_CE_CARE_RECEIVED_DETAILS tables.</summary>
    public class TravelingOutsideDTO
    {
        [JsonPropertyName("travelingOutSideCommunityForExtendedPeriod")]
        public bool? TravelingOutsideCommunity { get; set; }

        [Required]
        [JsonPropertyName("effectiveBeginDate")]
        public string? EffectiveBeginDate { get; set; }

        [JsonPropertyName("effectiveEndDate")]
        public string? EffectiveEndDate { get; set; }

        [JsonPropertyName("verificationSource")]
        public string? VerificationSource { get; set; }

        [JsonPropertyName("verificationDate")]
        public string? VerificationDate { get; set; }

        [JsonPropertyName("careReceivedDetails")]
        public List<CareReceivedDetailDTO>? CareReceivedDetails { get; set; }

        [Required]
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updateSw")]
        public string? UpdateSw { get; set; }

        [Required]
        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }
    }

    /// <summary>exemptions.substanceUseDisorderTreatment[].careReceivedDetails[] — persisted to the new
    /// MWRP_CE_TREATMENT_DETAILS table.</summary>
    public class TreatmentDetailDTO
    {
        [Required]
        [JsonPropertyName("treatmentStartDate")]
        public string? TreatmentStartDate { get; set; }

        [JsonPropertyName("treatmentEndDate")]
        public string? TreatmentEndDate { get; set; }
    }

    /// <summary>exemptions.substanceUseDisorderTreatment[] — persisted to the new
    /// MWRP_CE_SUBSTANCE_USE_DISORDER_TREATMENT / MWRP_CE_TREATMENT_DETAILS tables.</summary>
    public class SubstanceUseDisorderTreatmentDTO
    {
        [JsonPropertyName("substanceUseDisorderTreatment")]
        public bool? SubstanceUseDisorderTreatmentInd { get; set; }

        [Required]
        [JsonPropertyName("effectiveBeginDate")]
        public string? EffectiveBeginDate { get; set; }

        [JsonPropertyName("effectiveEndDate")]
        public string? EffectiveEndDate { get; set; }

        [JsonPropertyName("verificationSource")]
        public string? VerificationSource { get; set; }

        [JsonPropertyName("verificationDate")]
        public string? VerificationDate { get; set; }

        [JsonPropertyName("careReceivedDetails")]
        public List<TreatmentDetailDTO>? CareReceivedDetails { get; set; }

        [Required]
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updateSw")]
        public string? UpdateSw { get; set; }

        [Required]
        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }
    }

    /// <summary>exemptions.disabilityDetails[] — persisted to the new MWRP_CE_DISABILITY_DETAILS table.</summary>
    public class DisabilityDetailDTO
    {
        [Required]
        [JsonPropertyName("typeOfDisability")]
        public string? TypeOfDisability { get; set; }

        [Required]
        [JsonPropertyName("maVerification")]
        public string? MaVerification { get; set; }

        [Required]
        [JsonPropertyName("maDisabilityBeginDate")]
        public string? MaDisabilityBeginDate { get; set; }

        /// <summary>New in v1.7.4 — persisted to MWRP_CE_DISABILITY_DETAILS.MA_DISABILITY_END_DATE.</summary>
        [JsonPropertyName("maDisabilityEndDate")]
        public string? MaDisabilityEndDate { get; set; }

        [Required]
        [JsonPropertyName("disabilityOutcomeDeterminedByDDS")]
        public string? DisabilityOutcomeDeterminedByDds { get; set; }

        /// <summary>Misspelling ("disbility") is exactly as sent in docs/SamplePayload.json.</summary>
        [JsonPropertyName("disbilityOutcomeDeterminedByDDS")]
        public string? DisabilityOutcomeDeterminedByDdsMisspelled { set => DisabilityOutcomeDeterminedByDds ??= value; }

        [JsonPropertyName("updateSw")]
        public string? UpdateSw { get; set; }

        [Required]
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [Required]
        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }
    }

    /// <summary>exemptions.livingArrangementDetails[] — persisted to the new
    /// MWRP_CE_LIVING_ARRANGEMENT_DETAILS table.</summary>
    public class LivingArrangementDetailDTO
    {
        [Required]
        [JsonPropertyName("livingArrangementType")]
        public string? LivingArrangementType { get; set; }

        [Required]
        [JsonPropertyName("effectiveBeginDate")]
        public string? EffectiveBeginDate { get; set; }

        [JsonPropertyName("effectiveEndDate")]
        public string? EffectiveEndDate { get; set; }

        [JsonPropertyName("updateSw")]
        public string? UpdateSw { get; set; }

        [Required]
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [Required]
        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }
    }

    /// <summary>exemptions.circumstances[] / exemptions.circumstanceForExemption[] — persisted to the
    /// new MWRP_CE_CIRCUMSTANCE / MWRP_CE_CIRCUMSTANCE_DETAIL tables.</summary>
    public class CircumstanceDTO
    {
        [Required]
        [JsonPropertyName("circumstanceCode")]
        public string? CircumstanceCode { get; set; }

        [Required]
        [JsonPropertyName("circumstanceDescription")]
        public string? CircumstanceDescription { get; set; }

        [Required]
        [JsonPropertyName("circumstanceDetails")]
        public List<CircumstanceDetailDTO>? CircumstanceDetails { get; set; }
    }

    public class CircumstanceDetailDTO
    {
        [JsonPropertyName("verificationSource")]
        public string? VerificationSource { get; set; }

        [JsonPropertyName("verificationDate")]
        public string? VerificationDate { get; set; }

        [JsonPropertyName("effectiveBeginDate")]
        public string? EffectiveBeginDate { get; set; }

        [JsonPropertyName("effectiveEndDate")]
        public string? EffectiveEndDate { get; set; }

        [JsonPropertyName("blindOrDisabled")]
        public bool? BlindOrDisabled { get; set; }

        /// <summary>Replaces "individualComplyingWithTANFWorkParticipationRequirements" in v1.7.4.
        /// Persisted to MWRP_CE_CIRCUMSTANCE_DETAIL.COMPLY_WITH_TANF_WORK_IND.</summary>
        [JsonPropertyName("enrolledForTANF")]
        public bool? EnrolledForTanf { get; set; }

        /// <summary>Pre-v1.7.4 name still sent by the v1.7.3 QA payloads.</summary>
        [JsonPropertyName("individualComplyingWithTANFWorkParticipationRequirements")]
        public bool? IndividualComplyingWithTanfLegacy { set => EnrolledForTanf ??= value; }

        [JsonPropertyName("approvedForSNAPAndNotExemptFromSNAPWorkRequirement")]
        public bool? ApprovedForSnapNotExempt { get; set; }

        /// <summary>New in v1.7.4 (RT_EMPEXMTRSN) — persisted to MWRP_CE_CIRCUMSTANCE_DETAIL.SNAP_EXEMPT_TYPE.</summary>
        [JsonPropertyName("SNAPExemptionType")]
        public string? SnapExemptionType { get; set; }

        [Required]
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updateSw")]
        public string? UpdateSw { get; set; }

        [Required]
        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }
    }

    /// <summary>exemptions.pregnancy — replaces PregnancyDTO in v1.7.4. Persisted to MWRP_CE_PREGNANCY.</summary>
    public class PregnancyCircumstanceDTO
    {
        [Required]
        [JsonPropertyName("circumstanceCode")]
        public string? CircumstanceCode { get; set; }

        [Required]
        [JsonPropertyName("expectedPregnancyDueDate")]
        public string? ExpectedDueDate { get; set; }

        [JsonPropertyName("actualPregnancyEndDate")]
        public string? ActualPregnancyEndDate { get; set; }

        [JsonPropertyName("effectiveBeginDate")]
        public string? EffectiveBeginDate { get; set; }

        [JsonPropertyName("effectiveEndDate")]
        public string? EffectiveEndDate { get; set; }

        [Required]
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updatesw")]
        public string? UpdateSw { get; set; }

        [Required]
        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }
    }

    public class CaregiverExemptionDTO
    {
        [Required]
        [JsonPropertyName("circumstanceCode")]
        public string? CircumstanceCode { get; set; }

        [Required]
        [JsonPropertyName("relationshipWithDependent")]
        public string? RelationshipWithDependent { get; set; }

        [Required]
        [JsonPropertyName("dependentLiveInTheSameHome")]
        public bool? DependentLivesSameHome { get; set; }

        [JsonPropertyName("dependentDOB")]
        public string? DependentDob { get; set; }

        /// <summary>New in the 2026-08 revision — replaces the old scalar "caregivingHoursPerWeek" field.
        /// Renamed from "hoursOfCaregivingPerWeekMonth" to "hoursOfCaregivingPerMonth" in docs/SamplePayload.json.
        /// Persisted to the new MWRP_CE_CAREGIVER_MONTHLY_HOURS child table.</summary>
        [Required]
        [JsonPropertyName("hoursOfCaregivingPerMonth")]
        public List<CaregiverMonthlyHoursDTO>? HoursOfCaregivingPerWeekMonth { get; set; }

        [Required]
        [JsonPropertyName("isDependentDisabled")]
        public bool? IsDependentDisabled { get; set; }

        [JsonPropertyName("effectiveBeginDate")]
        public string? EffectiveBeginDate { get; set; }

        [JsonPropertyName("effectiveEndDate")]
        public string? EffectiveEndDate { get; set; }

        [Required]
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updatesw")]
        public string? UpdateSw { get; set; }

        [Required]
        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }
    }

    public class CaregiverMonthlyHoursDTO
    {
        [Required]
        [JsonPropertyName("monthAndYear")]
        public string? MonthAndYear { get; set; }

        [Required]
        [JsonPropertyName("monthlyHours")]
        public decimal? MonthlyHours { get; set; }
    }

    public class FormerFosterCareExemptionDTO
    {
        [Required]
        [JsonPropertyName("circumstanceCode")]
        public string? CircumstanceCode { get; set; }

        [Required]
        [JsonPropertyName("enrolledInMedicaidAndFosterCareAtAge18")]
        public bool? EnrolledMedicaidFosterCareAge18 { get; set; }

        /// <summary>New in the 2026-08 revision — persisted to CE_EXCLUSION_FORMER_FOSTER_CARE.AGE_QTY.</summary>
        [Required]
        [JsonPropertyName("age")]
        public int? Age { get; set; }

        [JsonPropertyName("effectiveBeginDate")]
        public string? EffectiveBeginDate { get; set; }

        [JsonPropertyName("effectiveEndDate")]
        public string? EffectiveEndDate { get; set; }

        /// <summary>New in the 2026-08 revision — persisted to MWRP_CE_FORMER_FOSTER_CARE.AID_CATEGORY_WHEN_TURNED_18
        /// (VARCHAR(30) in v1.7.2), so the aid-category code string is stored as-is.</summary>
        [JsonPropertyName("aidCategoryWhenTurned18")]
        [JsonConverter(typeof(FlexibleStringConverter))]
        public string? AidCategoryWhenTurned18 { get; set; }

        /// <summary>New in docs/SamplePayload.json — persisted to CE_FORMER_FOSTER_CARE.CLIENT_RECEIVED_FORMER_FOSTER_CARE.</summary>
        [Required]
        [JsonPropertyName("clientReceivedFormerFosterCare")]
        public string? ClientReceivedFormerFosterCare { get; set; }

        [Required]
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updatesw")]
        public string? UpdateSw { get; set; }

        [Required]
        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }
    }

    /// <summary>Item of the "incarceratedCircumstance" array (docs/SamplePayload.json; was a single
    /// "currentlyIncarcerated" object). "toa"/"livingArrangementType"/"incarceratedLast3Months" have no
    /// corresponding column on MWRP_CE_INCARCERATED_CIRCUMSTANCE and are not modeled.</summary>
    public class IncarcerationExemptionDTO
    {
        [Required]
        [JsonPropertyName("circumstanceCode")]
        public string? CircumstanceCode { get; set; }

        [Required]
        [JsonPropertyName("aidCategory")]
        public string? AidCategory { get; set; }

        [JsonPropertyName("facilityType")]
        public string? FacilityType { get; set; }

        [JsonPropertyName("effectiveBeginDate")]
        public string? EffectiveBeginDate { get; set; }

        [JsonPropertyName("effectiveEndDate")]
        public string? EffectiveEndDate { get; set; }

        /// <summary>Renamed from "releasedDate" to "releaseDate" in docs/SamplePayload.json.</summary>
        [JsonPropertyName("releaseDate")]
        public string? ReleasedDate { get; set; }

        [Required]
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updatesw")]
        public string? UpdateSw { get; set; }

        [Required]
        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }
    }

    /// <summary>Item of the "receivingMedicarePartAB" array (ICD MedicarePartABDTO).</summary>
    public class MedicarePartAbExemptionDTO
    {
        /// <summary>New in v1.7.4 (RT_DCINSURANCETYPE), replacing typeOfCoverage / medicareExpenseType /
        /// overallTypeOfCoverage / isSOLQ. Persisted to MWRP_CE_MEDICARE_PART_AB.MEDICARE_TYPE_CD.</summary>
        [Required]
        [JsonPropertyName("entitledToOrReceivingMedicareType")]
        public string? EntitledToOrReceivingMedicareType { get; set; }

        /// <summary>Removed from the ICD in v1.7.4, but MWRP_CE_MEDICARE_PART_AB.TYPE_OF_COVERAGE still exists,
        /// so it is persisted when a sender still includes it.</summary>
        [JsonPropertyName("typeOfCoverage")]
        public string? CoverageType { get; set; }

        [Required]
        [JsonPropertyName("startDate")]
        public string? StartDate { get; set; }

        [JsonPropertyName("endDate")]
        public string? EndDate { get; set; }

        [Required]
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updatesw")]
        public string? UpdateSw { get; set; }

        [Required]
        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }
    }

    /// <summary>Backs the "medicallyFrail" exemption (was "frailty"); now an array in docs/SamplePayload.json.</summary>
    public class FrailtyExemptionDTO
    {
        [JsonPropertyName("circumstanceCode")]
        public string? CircumstanceCode { get; set; }

        /// <summary>Sent as a boolean flag in docs/SamplePayload.json (was previously modeled as free text).</summary>
        [Required]
        [JsonPropertyName("seriousMedicalCondition")]
        public bool? SeriousMedicalCondition { get; set; }

        [JsonPropertyName("seriousHealthProblemImpactsYourLife")]
        public bool? SeriousHealthImpact { get; set; }

        [JsonPropertyName("mentalHealth")]
        public bool? MentalHealth { get; set; }

        [JsonPropertyName("physicalDisability")]
        public bool? PhysicalDisability { get; set; }

        /// <summary>No longer in the v1.7.4 FrailtyDTO, but MWRP_CE_FRAILTY.VERIFICATION_SOURCE still exists.</summary>
        [JsonPropertyName("verificationSource")]
        public string? VerificationSource { get; set; }

        /// <summary>Still listed (optional) on the v1.7.4 FrailtyDTO; v1.7.4 also adds exemptions.indvCBCTypeDetails.
        /// Both feed MWRP_CE_CBC_TYPE_DETAILS.</summary>
        [JsonPropertyName("cBCTypeDetails")]
        public List<IndvCbcTypeDetailDTO>? CbcTypeDetails { get; set; }

        [Required]
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

        /// <summary>Persisted to CE_FRAILTY.LTSS_SCREENING_IND. Optional since v1.7.4.</summary>
        [JsonPropertyName("lTSSScreening")]
        public bool? LtssScreening { get; set; }

        /// <summary>Moved to exemptions.solqTitleDisability in v1.7.4, but MWRP_CE_FRAILTY.SOLQ_TITLE_II_DISABLED_IND
        /// still exists, so it is persisted when a sender still includes it.</summary>
        [JsonPropertyName("sOLQTitleIIDisabled")]
        public bool? SolqTitleIIDisabled { get; set; }

        /// <summary>Same as <see cref="SolqTitleIIDisabled"/>, for SOLQ_TITLE_XVI_DISABLED_IND.</summary>
        [JsonPropertyName("sOLQTitleXVIDisabled")]
        public bool? SolqTitleXviDisabled { get; set; }

        [Required]
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updatesw")]
        public string? UpdateSw { get; set; }

        [Required]
        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }
    }

    /// <summary>exemptions.indvCBCTypeDetails[] (ICD IndvCBCTypeDetailsDTO, replacing CBCTypeDetailsDTO in v1.7.4)
    /// and medicallyFrail[].cBCTypeDetails[] — persisted to MWRP_CE_CBC_TYPE_DETAILS.</summary>
    public class IndvCbcTypeDetailDTO
    {
        [Required]
        [JsonPropertyName("waiverSw")]
        public string? WaiverSw { get; set; }

        [Required]
        [JsonPropertyName("paceCbcType")]
        public string? PaceCbcType { get; set; }

        [JsonPropertyName("cbcType")]
        public string? CbcType { get; set; }

        [Required]
        [JsonPropertyName("maBeginDate")]
        public string? MaBeginDate { get; set; }

        [JsonPropertyName("maEndDate")]
        public string? MaEndDate { get; set; }

        [JsonPropertyName("updateSw")]
        public string? UpdateSw { get; set; }

        [Required]
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [Required]
        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }
    }

    /// <summary>exemptions.solqTitleDisability[] (new in v1.7.4) — persisted to MWRP_CE_SOLQ_TITLE_DISABLED.</summary>
    public class SolqTitleDisabledDTO
    {
        /// <summary>SOLQTYPE: "SOLQ Title II" or "SOLQ Title XVI".</summary>
        [Required]
        [JsonPropertyName("sOLQTitleDisabledType")]
        public string? SolqTitleDisabledType { get; set; }

        [Required]
        [JsonPropertyName("effectiveBeginDate")]
        public string? EffectiveBeginDate { get; set; }

        [JsonPropertyName("effectiveEndDate")]
        public string? EffectiveEndDate { get; set; }

        [Required]
        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }

        [JsonPropertyName("updateSw")]
        public string? UpdateSw { get; set; }

        [Required]
        [JsonPropertyName("source")]
        public string? Source { get; set; }
    }

    /// <summary>exemptions.lTSSScreeningDetails[] (new in v1.7.4) — persisted to MWRP_CE_LTSS_SCREENING.</summary>
    public class LtssScreeningDTO
    {
        [Required]
        [JsonPropertyName("lTSSScreening")]
        public bool? LtssScreening { get; set; }

        [Required]
        [JsonPropertyName("effectiveBeginDate")]
        public string? EffectiveBeginDate { get; set; }

        [JsonPropertyName("effectiveEndDate")]
        public string? EffectiveEndDate { get; set; }

        [Required]
        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }

        [JsonPropertyName("updateSw")]
        public string? UpdateSw { get; set; }

        [Required]
        [JsonPropertyName("source")]
        public string? Source { get; set; }
    }

    /// <summary>New in the 2026-08 revision — persisted to the new MWRP_CE_VETERAN_DISABILITY_CIRCUMSTANCE table.</summary>
    public class VeteranDisabilityExemptionDTO
    {
        [Required]
        [JsonPropertyName("circumstanceCode")]
        public string? CircumstanceCode { get; set; }

        [JsonPropertyName("verificationSource")]
        public string? VerificationSource { get; set; }

        [JsonPropertyName("verificationDate")]
        public string? VerificationDate { get; set; }

        [JsonPropertyName("veteranWith100percentDisability")]
        public bool? VeteranWith100PercentDisability { get; set; }

        /// <summary>New in docs/SamplePayload.json — persisted to CE_VETERAN_DISABILITY_CIRCUMSTANCE.VETERAN_DISABILITY_SUBTYPE.</summary>
        [JsonPropertyName("veteranDisabilitySubtypes")]
        public string? VeteranDisabilitySubtype { get; set; }

        [JsonPropertyName("effectiveBeginDate")]
        public string? EffectiveBeginDate { get; set; }

        [JsonPropertyName("effectiveEndDate")]
        public string? EffectiveEndDate { get; set; }

        [Required]
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updatesw")]
        public string? UpdateSw { get; set; }

        [Required]
        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }
    }

    // ─── Engagements ──────────────────────────────────────────────────────────────

    public class EngagementsDTO
    {
        /// <summary>Renamed from "employment" to "incomeDetails" in docs/SamplePayload.json.</summary>
        [JsonPropertyName("incomeDetails")]
        public List<EmploymentEntryDTO>? Employment { get; set; }

        [JsonPropertyName("jobTraining")]
        public List<JobTrainingEntryDTO>? JobTraining { get; set; }

        [JsonPropertyName("education")]
        public List<EducationEntryDTO>? Education { get; set; }

        /// <summary>Simple verified income/hours summary — feeds MWRP_CE_ELECTRONICALLY_VERIFIED_EMPLOYMENT.
        /// (Not to be confused with the richer Truv tree, which now lives at employment[].electronicallyVerifiedData.)</summary>
        [JsonPropertyName("electronicallyVerifiedEmployment")]
        public List<ElectronicallyVerifiedEmploymentDTO>? ElectronicallyVerifiedEmployment { get; set; }

        /// <summary>Persisted to the MWRP_CE_EV_INDV_INCOME_BUDGET table.</summary>
        [JsonPropertyName("evIndvIncomeBudgetForBudgetGroupMembers")]
        public List<IndvIncomeBudgetDTO>? EvIndvIncomeBudget { get; set; }

        /// <summary>Older name still sent by docs/SamplePayload.json.</summary>
        [JsonPropertyName("evIndvIncomeBudget")]
        public List<IndvIncomeBudgetDTO>? EvIndvIncomeBudgetLegacy { set => EvIndvIncomeBudget ??= value; }

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

        /// <summary>Persisted to MWRP_CE_INKIND_UNPAID_WORK. Optional since v1.7.4.</summary>
        [JsonPropertyName("inKindOrUnpaidWork")]
        public List<InKindOrUnpaidWorkEntryDTO>? InKindOrUnpaidWork { get; set; }

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

        /// <summary>New in docs/SamplePayload.json — persisted to CE_INCOME_DETAILS.INCOME_TYPE.</summary>
        [Required]
        [JsonPropertyName("incomeType")]
        public string? IncomeType { get; set; }

        [JsonPropertyName("employmentType")]
        public string? EmploymentType { get; set; }

        /// <summary>Persisted to MWRP_CE_INCOME_DETAILS.UNEMPLOYMENT_TYPE.</summary>
        [JsonPropertyName("unEmploymentType")]
        public string? UnemploymentType { get; set; }

        [JsonPropertyName("isSeasonalEmployment")]
        public bool? IsSeasonalEmployment { get; set; }

        [JsonPropertyName("seasonalEmploymentType")]
        public string? SeasonalEmploymentType { get; set; }

        [Required]
        [JsonPropertyName("isPriorEmployment")]
        public bool? IsPriorEmployment { get; set; }

        /// <summary>Name sent by docs/SamplePayload.json instead of the ICD's "isPriorEmployment".</summary>
        [JsonPropertyName("priorOrCurrentEmployment")]
        public bool? PriorOrCurrentEmploymentLegacy { set => IsPriorEmployment ??= value; }

        [JsonPropertyName("startDate")]
        public string? StartDate { get; set; }

        [JsonPropertyName("endDate")]
        public string? EndDate { get; set; }

        [JsonPropertyName("isEmploymentActive")]
        public bool? IsEmploymentActive { get; set; }

        [JsonPropertyName("isInkindOrUnPaid")]
        public bool? IsInkindOrUnPaid { get; set; }

        /// <summary>Now an array in the 2026-08 revision (was a single object).</summary>
        [Required]
        [JsonPropertyName("incomeBudgetDetails")]
        public List<IncomeBudgetDetailsDTO>? IncomeBudgetDetails { get; set; }

        /// <summary>New in the 2026-08 revision — the Truv/payroll-provider verified employer tree now
        /// nests here (per employment entry) instead of at the top level of "engagements".</summary>
        [JsonPropertyName("electronicallyVerifiedData")]
        public List<TruvEmployerDTO>? ElectronicallyVerifiedData { get; set; }

        [Required]
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updateSw")]
        public string? UpdateSw { get; set; }

        [Required]
        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }
    }

    public class IncomeBudgetDetailsDTO
    {
        [Required]
        [JsonPropertyName("monthlyIncome")]
        public decimal? MonthlyIncome { get; set; }

        [Required]
        [JsonPropertyName("monthlyHours")]
        public decimal? MonthlyHours { get; set; }

        [Required]
        [JsonPropertyName("incomeMonth")]
        public int? IncomeMonth { get; set; }

        [Required]
        [JsonPropertyName("incomeYear")]
        public int? IncomeYear { get; set; }

        [Required]
        [JsonPropertyName("verificationSource")]
        public string? VerificationSource { get; set; }

        [Required]
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updatesw")]
        public string? UpdateSw { get; set; }

        [Required]
        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }
    }

    /// <summary>Simple verified income/hours summary item — feeds MWRP_CE_ELECTRONICALLY_VERIFIED_EMPLOYMENT.</summary>
    public class ElectronicallyVerifiedEmploymentDTO
    {
        [Required]
        [JsonPropertyName("verificationSource")]
        public string? VerificationSource { get; set; }

        [Required]
        [JsonPropertyName("monthlyIncome")]
        public decimal? MonthlyIncome { get; set; }

        /// <summary>Optional since v1.7.4.</summary>
        [JsonPropertyName("monthlyHours")]
        public decimal? MonthlyHours { get; set; }

        [Required]
        [JsonPropertyName("incomeMonth")]
        public int? IncomeMonth { get; set; }

        [Required]
        [JsonPropertyName("incomeYear")]
        public int? IncomeYear { get; set; }
    }

    /// <summary>engagements.evIndvIncomeBudget[] — persisted to the new MWRP_CE_INDV_INCOME_BUDGET table.</summary>
    public class IndvIncomeBudgetDTO
    {
        [Required]
        [JsonPropertyName("clientId")]
        public long? ClientId { get; set; }

        [JsonPropertyName("ein")]
        [JsonConverter(typeof(FlexibleStringConverter))]
        public string? Ein { get; set; }

        [Required]
        [JsonPropertyName("budgetBeginDt")]
        public string? BudgetBeginDate { get; set; }

        [JsonPropertyName("budgetEndDt")]
        public string? BudgetEndDate { get; set; }

        /// <summary>Required since v1.7.4 — persisted to CE_EV_INDV_INCOME_BUDGET.SOURCE_SYSTEM.</summary>
        [Required]
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        /// <summary>Persisted to CE_EV_INDV_INCOME_BUDGET.VERIFICATION_SOURCE.</summary>
        [Required]
        [JsonPropertyName("verificationSource")]
        public string? VerificationSource { get; set; }

        [Required]
        [JsonPropertyName("totalBudgetAmount")]
        public decimal? TotalBudgetAmount { get; set; }

        /// <summary>New and required in v1.7.4 — persisted to CE_EV_INDV_INCOME_BUDGET.TOTAL_BUDGET_HOURS.</summary>
        [Required]
        [JsonPropertyName("totalBudgetHours")]
        public decimal? TotalBudgetHours { get; set; }
    }

    /// <summary>engagements.evEdgIncomeBudget[] and engagements.magiIncomeBudget[] (same shape) — persisted
    /// to the new MWRP_CE_EDG_INCOME_BUDGET / MWRP_CE_MAGI_INCOME_BUDGET tables respectively.</summary>
    public class EdgIncomeBudgetDTO
    {
        [Required]
        [JsonPropertyName("edgNum")]
        public int? EdgNum { get; set; }

        [Required]
        [JsonPropertyName("eligBeginDt")]
        public string? EligBeginDate { get; set; }

        [JsonPropertyName("eligEndDt")]
        public string? EligEndDate { get; set; }

        [Required]
        [JsonPropertyName("totalBudgetAmount")]
        public decimal? TotalBudgetAmount { get; set; }
    }

    /// <summary>engagements.budgetGroupMembers[] — persisted to the new MWRP_CE_BUDGET_GROUP_MEMBER table.</summary>
    public class BudgetGroupMemberDTO
    {
        [Required]
        [JsonPropertyName("edgNum")]
        public int? EdgNum { get; set; }

        [Required]
        [JsonPropertyName("clientId")]
        public long? ClientId { get; set; }

        /// <summary>Fixes a typo ("EligParfStatusCd") — docs/SamplePayload.json sends "eligPartStatusCd".
        /// NOTE: MWRP_CE_BUDGET_GROUP_MEMBER.ELIG_PART_STATUS_CD is typed DATE in docs/Table_v17.txt, but
        /// the payload sends a status code string (e.g. "ACTIVE"), which cannot be parsed as a date. This
        /// value will not persist until the column type or the payload contract is reconciled.</summary>
        [JsonPropertyName("eligPartStatusCd")]
        public string? EligPartStatusCd { get; set; }
    }

    // ─── Job Training / In-Kind or Unpaid Work (CE_JOB_TRAINING / CE_INKIND_UNPAID_WORK) ─────

    /// <summary>engagements.jobTraining[] — the in-kind shape plus a programType the ICD requires here only.</summary>
    public class JobTrainingEntryDTO : InKindOrUnpaidWorkEntryDTO, IValidatableObject
    {
        /// <summary>Persisted to MWRP_CE_JOB_TRAINING.PROGRAM_TYPE (MWRP_CE_INKIND_UNPAID_WORK has no such column).</summary>
        [Required]
        [JsonPropertyName("programType")]
        public string? ProgramType { get; set; }

        /// <summary>v1.7.4 makes these required for job training only; they stay optional for in-kind/unpaid work.</summary>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(OrganizationName))
                yield return new ValidationResult("organizationName is required.", new[] { nameof(OrganizationName) });
            if (MonthlyHours == null)
                yield return new ValidationResult("monthlyHours is required.", new[] { nameof(MonthlyHours) });
            if (string.IsNullOrWhiteSpace(StartDate))
                yield return new ValidationResult("startDate is required.", new[] { nameof(StartDate) });
        }
    }

    /// <summary>engagements.inKindOrUnpaidWork[] (ICD IsInkindOrUnPaidResultDTO).</summary>
    public class InKindOrUnpaidWorkEntryDTO
    {
        /// <summary>Sent as a non-numeric string (e.g. "ORG-JT-001") in docs/SamplePayload.json.</summary>
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

        [Required]
        [JsonPropertyName("verificationSource")]
        public string? VerificationSource { get; set; }

        [Required]
        [JsonPropertyName("programName")]
        public string? ProgramName { get; set; }

        [Required]
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updateSw")]
        public string? UpdateSw { get; set; }

        [Required]
        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }
    }

    // ─── Volunteering (CE_VOLUNTEER_SERVICE) ─────────────────────────────────────

    public class VolunteeringEntryDTO
    {
        [JsonPropertyName("organizationID")]
        public string? OrganizationId { get; set; }

        [Required]
        [JsonPropertyName("organizationName")]
        public string? OrganizationName { get; set; }

        [Required]
        [JsonPropertyName("monthlyHours")]
        public decimal? MonthlyHours { get; set; }

        [Required]
        [JsonPropertyName("startDate")]
        public string? StartDate { get; set; }

        [JsonPropertyName("endDate")]
        public string? EndDate { get; set; }

        [Required]
        [JsonPropertyName("verificationSource")]
        public string? VerificationSource { get; set; }

        [JsonPropertyName("verificationDate")]
        public string? VerificationDate { get; set; }

        [Required]
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updatesw")]
        public string? UpdateSw { get; set; }

        [Required]
        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }
    }

    // ─── Education (CE_EDUCATION / CE_NSC_ENROLLMENT / CE_NSC_ENROLLMENT_DETAIL /
    //    CE_COURSE_OF_STUDY) ───────────────────────────────────────────────────────

    public class EducationEntryDTO
    {
        /// <summary>New and required in v1.7.4 — persisted to MWRP_CE_EDUCATION_VERIFIED.INSTITUTION_CODE.</summary>
        [Required]
        [JsonPropertyName("institutionCode")]
        [JsonConverter(typeof(FlexibleStringConverter))]
        public string? InstitutionCode { get; set; }

        [Required]
        [JsonPropertyName("institutionName")]
        public string? InstitutionName { get; set; }

        [JsonPropertyName("institutionType")]
        public string? InstitutionType { get; set; }

        [JsonPropertyName("verificationDate")]
        public string? VerificationDate { get; set; }

        /// <summary>Optional since v1.7.4.</summary>
        [JsonPropertyName("verificationSource")]
        public string? VerificationSource { get; set; }

        [Required]
        [JsonPropertyName("enrollmentStatus")]
        public string? EnrollmentStatus { get; set; }

        /// <summary>New and required in v1.7.4 — persisted to MWRP_CE_EDUCATION_VERIFIED.CREDIT_HOURS.</summary>
        [Required]
        [JsonPropertyName("creditHours")]
        public decimal? CreditHours { get; set; }

        /// <summary>Optional since v1.7.4.</summary>
        [JsonPropertyName("monthlyHours")]
        public decimal? MonthlyHours { get; set; }

        [Required]
        [JsonPropertyName("termStartDate")]
        public string? TermStartDate { get; set; }

        /// <summary>Optional since v1.7.4.</summary>
        [JsonPropertyName("termEndDate")]
        public string? TermEndDate { get; set; }

        [JsonPropertyName("electronicallyVerifiedData")]
        public List<NscEnrollmentDTO>? ElectronicallyVerifiedData { get; set; }

        [Required]
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updateSw")]
        public string? UpdateSw { get; set; }

        [Required]
        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }
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

        [Required]
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updateSw")]
        public string? UpdateSw { get; set; }

        [Required]
        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }
    }

    public class CourseOfStudyDTO
    {
        [JsonPropertyName("course")]
        public string? Course { get; set; }

        [JsonPropertyName("ncesCIPCode")]
        [JsonConverter(typeof(FlexibleStringConverter))]
        public string? NcesCipCode { get; set; }

        [Required]
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updateSw")]
        public string? UpdateSw { get; set; }

        [Required]
        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }
    }

    // ─── Truv Verified Employment (CE_TRUV_EMPLOYER / CE_TRUV_PROVIDER /
    //    CE_EMPLOYMENT_DETAIL / CE_EMPLOYEE_PROFILE / CE_PAY_STATEMENT /
    //    CE_ANNUAL_INCOME) ─────────────────────────────────────────────────────────
    // The tree nested under incomeDetails[].electronicallyVerifiedData in
    // docs/SamplePayload.json uses snake_case field names throughout (this is the raw
    // shape Truv itself returns), while the "profile" sub-object keeps camelCase.
    // All "id" fields are sent as non-numeric strings (e.g. "prov-001", "emp-1001"),
    // so they are modeled as string, matching the VARCHAR id columns in Table_v17.txt.
    // Several numeric fields (income, payRate, pay-statement amounts) arrive as
    // quoted JSON strings; Program.cs enables JsonNumberHandling.AllowReadingFromString
    // so they still bind to decimal?/int? DTO properties without a custom converter.

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

        [Required]
        [JsonPropertyName("company_name")]
        public string? CompanyName { get; set; }

        [JsonPropertyName("is_suspicious")]
        public bool? IsSuspicious { get; set; }

        [JsonPropertyName("start_date")]
        public string? StartDate { get; set; }

        [JsonPropertyName("end_date")]
        public string? EndDate { get; set; }

        [JsonPropertyName("provider")]
        public TruvProviderDTO? Provider { get; set; }

        [JsonPropertyName("employments")]
        public List<TruvEmploymentDetailDTO>? Employments { get; set; }

        // The ICD specifies snake_case, but senders also post camelCase; both must bind.
        [JsonPropertyName("productType")]
        public string? ProductTypeCamelCase { set => ProductType ??= value; }

        [JsonPropertyName("dataSource")]
        public string? DataSourceCamelCase { set => DataSource ??= value; }

        [JsonPropertyName("companyName")]
        public string? CompanyNameCamelCase { set => CompanyName ??= value; }

        [JsonPropertyName("isSuspicious")]
        public bool? IsSuspiciousCamelCase { set => IsSuspicious ??= value; }

        [JsonPropertyName("startDate")]
        public string? StartDateCamelCase { set => StartDate ??= value; }

        [JsonPropertyName("endDate")]
        public string? EndDateCamelCase { set => EndDate ??= value; }
    }

    public class TruvProviderDTO
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("logo_url")]
        public string? LogoUrl { get; set; }

        // The ICD specifies snake_case, but senders also post camelCase; both must bind.
        [JsonPropertyName("logoUrl")]
        public string? LogoUrlCamelCase { set => LogoUrl ??= value; }
    }

    public class TruvEmploymentDetailDTO
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

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

        [JsonPropertyName("income")]
        public decimal? Income { get; set; }

        [JsonPropertyName("income_unit")]
        public string? IncomeUnit { get; set; }

        [JsonPropertyName("pay_rate")]
        public decimal? PayRate { get; set; }

        [JsonPropertyName("pay_frequency")]
        public string? PayFrequency { get; set; }

        /// <summary>Stays camelCase in docs/SamplePayload.json even inside the otherwise snake_case Truv tree.</summary>
        [JsonPropertyName("profile")]
        public TruvEmployeeProfileDTO? Profile { get; set; }

        /// <summary>Renamed from singular "statement" to plural "statements" in docs/SamplePayload.json.</summary>
        [JsonPropertyName("statements")]
        public List<TruvPayStatementDTO>? Statement { get; set; }

        [JsonPropertyName("annual_income_summary")]
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

        // The ICD names this field logo_url; SamplePayload.json sends logoUrl. Both must bind.
        [JsonPropertyName("logo_url")]
        public string? LogoUrlSnakeCase { set => LogoUrl ??= value; }
    }

    public class TruvPayStatementDTO
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

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
        public decimal? OverTime { get; set; }

        [JsonPropertyName("overtime_ytd")]
        public decimal? OverTimeYtd { get; set; }
    }

    public class TruvAnnualIncomeDTO
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("year")]
        public int? Year { get; set; }

        [JsonPropertyName("regular")]
        public decimal? Regular { get; set; }

        [JsonPropertyName("bonus")]
        public decimal? Bonus { get; set; }

        [JsonPropertyName("commission")]
        public decimal? Commission { get; set; }

        [JsonPropertyName("overtime")]
        public decimal? OverTime { get; set; }

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
