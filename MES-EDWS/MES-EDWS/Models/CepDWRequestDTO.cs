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
        /// is TIMESTAMP(6) WITH TIME ZONE, so it is written via <see cref="TryGetRequestTimestamp"/>
        /// as a DateTimeOffset, which preserves the offset.
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

        [JsonPropertyName("caseNumber")]
        public string? CaseNumber { get; set; }

        [JsonPropertyName("sendEmailToCustomer")]
        public bool? SendEmailToCustomer { get; set; }

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

        /// <summary>Documents from both the current and the legacy segment name.</summary>
        public IEnumerable<DocumentUploadDTO> AllDocumentsUploaded() =>
            (DocumentsUploadedIn ?? Enumerable.Empty<DocumentUploadDTO>())
                .Concat(DocumentsUploadedInMwrp ?? Enumerable.Empty<DocumentUploadDTO>());

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
        [JsonPropertyName("documentType")]
        public string? DocumentType { get; set; }

        [JsonPropertyName("documentSubType")]
        public string? DocumentSubtype { get; set; }

        /// <summary>Sent as a quoted string in the v1.7.3 payload; persisted to the VARCHAR CLIENT_ID column.</summary>
        [JsonPropertyName("clientId")]
        [JsonConverter(typeof(FlexibleStringConverter))]
        public string? ClientId { get; set; }

        /// <summary>Sent as a quoted string in the v1.7.3 payload; DOCUMENT_ID is VARCHAR(50) in v1.7.2.</summary>
        [JsonPropertyName("documentId")]
        [JsonConverter(typeof(FlexibleStringConverter))]
        public string? DocumentId { get; set; }

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
        /// <summary>Renamed from "clientIdentificationNumber" to "clientId" in docs/SamplePayload.json.</summary>
        [JsonPropertyName("clientId")]
        public string? ClientIdentificationNumber { get; set; }

        [JsonPropertyName("firstName")]
        public string? FirstName { get; set; }

        [JsonPropertyName("lastName")]
        public string? LastName { get; set; }

        [JsonPropertyName("middleName")]
        public string? MiddleName { get; set; }

        [JsonPropertyName("suffix")]
        public string? Suffix { get; set; }

        [JsonPropertyName("gender")]
        public string? Gender { get; set; }

        [JsonPropertyName("dateOfBirth")]
        public string? DateOfBirth { get; set; }

        [JsonPropertyName("socialSecurityNumber")]
        public string? SocialSecurityNumber { get; set; }

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

        [JsonPropertyName("caseAction")]
        public string? CaseAction { get; set; }

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

        [JsonPropertyName("primaryModeOfCommunication")]
        public string? PrimaryModeOfCommunication { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("alternateEmail")]
        public string? AlternateEmail { get; set; }

        /// <summary>New in docs/SamplePayload.json — persisted to CE_VERIFIED.VERIFICATION_TYPE.</summary>
        [JsonPropertyName("verificationType")]
        public string? VerificationType { get; set; }

        [JsonPropertyName("phoneData")]
        public List<PhoneDataDTO>? PhoneData { get; set; }

        [JsonPropertyName("addresses")]
        public List<AddressDTO>? Addresses { get; set; }

        [JsonPropertyName("headOfHousehold")]
        public bool? HeadOfHousehold { get; set; }

        [JsonPropertyName("requiresCEEvaluation")]
        public bool? RequiresCeEvaluation { get; set; }

        /// <summary>New in docs/SamplePayload.json — persisted to the MWRP_CE_RELATIONSHIP_DETAILS table.</summary>
        [JsonPropertyName("relationshipDetails")]
        public List<RelationshipDetailDTO>? RelationshipDetails { get; set; }

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

    /// <summary>ceVerified[].relationshipDetails[] — persisted to the new MWRP_CE_RELATIONSHIP_DETAILS table.</summary>
    public class RelationshipDetailDTO
    {
        [JsonPropertyName("reverseClientId")]
        public long? ReverseClientId { get; set; }

        [JsonPropertyName("relationType")]
        public string? RelationType { get; set; }

        [JsonPropertyName("effectiveBeginDate")]
        public string? EffectiveBeginDate { get; set; }

        [JsonPropertyName("effectiveEndDate")]
        public string? EffectiveEndDate { get; set; }

        [JsonPropertyName("updateSw")]
        public string? UpdateSw { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }
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
        /// <summary>JSON field is named "aidCategory" (not "code") in the 2026-08 revision. Sent as a
        /// quoted string (e.g. "102") and persisted as-is to the VARCHAR(20) AID_CATEGORY column.</summary>
        [JsonPropertyName("aidCategory")]
        [JsonConverter(typeof(FlexibleStringConverter))]
        public string? Code { get; set; }

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

        /// <summary>Sent as a quoted string (e.g. "760"); FIPS_CODE is VARCHAR(20), so it is persisted as-is.</summary>
        [JsonPropertyName("fipsCode")]
        [JsonConverter(typeof(FlexibleStringConverter))]
        public string? FipsCode { get; set; }

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

        /// <summary>Renamed from "currentlyIncarcerated" to "incarceratedCircumstance" and is now an array
        /// in docs/SamplePayload.json.</summary>
        [JsonPropertyName("incarceratedCircumstance")]
        public List<IncarcerationExemptionDTO>? CurrentlyIncarcerated { get; set; }

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
        [JsonPropertyName("livingArrangementDetails")]
        public List<LivingArrangementDetailDTO>? LivingArrangementDetails { get; set; }

        /// <summary>New in docs/SamplePayload.json — persisted to the new MWRP_CE_CIRCUMSTANCE /
        /// MWRP_CE_CIRCUMSTANCE_DETAIL tables.</summary>
        [JsonPropertyName("circumstanceForExemption")]
        public List<CircumstanceDTO>? CircumstanceForExemption { get; set; }

        /// <summary>Now a rich array (was previously assumed to only carry lineage fields) — persisted to
        /// the same MWRP_CE_CIRCUMSTANCE / MWRP_CE_CIRCUMSTANCE_DETAIL tables as "circumstanceForExemption".</summary>
        [JsonPropertyName("circumstances")]
        public List<CircumstanceDTO>? Circumstances { get; set; }
    }

    /// <summary>exemptions.americanIndianAlaskanNativeCircumstance — persisted to the new
    /// MWRP_CE_AMERICAN_INDIAN_ALASKAN_NATIVE table.</summary>
    public class AmericanIndianAlaskanNativeDTO
    {
        [JsonPropertyName("circumstanceCode")]
        public string? CircumstanceCode { get; set; }

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

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updateSw")]
        public string? UpdateSw { get; set; }

        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }

        [JsonPropertyName("applicationSource")]
        public string? ApplicationSource { get; set; }
    }

    /// <summary>exemptions.fosterCare — persisted to the new MWRP_CE_FOSTER_CARE table.</summary>
    public class FosterCareExemptionDTO
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

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updateSw")]
        public string? UpdateSw { get; set; }

        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }
    }

    /// <summary>exemptions.priorIncarcerationDetails[] — persisted to the new
    /// MWRP_CE_PRIOR_INCARCERATION_DETAIL table.</summary>
    public class PriorIncarcerationDetailDTO
    {
        [JsonPropertyName("wasThereIncarcerationInLast4Months")]
        public string? WasIncarceratedLast4Months { get; set; }

        [JsonPropertyName("priorIncarcerationEndDate")]
        public string? PriorIncarcerationEndDate { get; set; }

        [JsonPropertyName("priorIncarcerationType")]
        public string? PriorIncarcerationType { get; set; }
    }

    /// <summary>exemptions.receivingMedicarePartABTPL[] — persisted to the new
    /// MWRP_CE_MEDICARE_PART_AB_TPL table.</summary>
    public class MedicarePartAbTplExemptionDTO
    {
        [JsonPropertyName("typeOfCoverage")]
        public string? CoverageType { get; set; }

        [JsonPropertyName("overAllTypeOfCoverage")]
        public string? OverallTypeOfCoverage { get; set; }

        [JsonPropertyName("isSOLQ")]
        public bool? Solq { get; set; }

        [JsonPropertyName("startDate")]
        public string? StartDate { get; set; }

        [JsonPropertyName("endDate")]
        public string? EndDate { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updateSw")]
        public string? UpdateSw { get; set; }

        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }
    }

    /// <summary>Shared shape for exemptions.medicalExemptions[].careReceivedDetails[] and
    /// exemptions.travelingOutside[].careReceivedDetails[] — persisted to the
    /// MWRP_CE_CARE_RECEIVED_DETAILS table.</summary>
    public class CareReceivedDetailDTO
    {
        [JsonPropertyName("typeOfCare")]
        public string? TypeOfCare { get; set; }

        [JsonPropertyName("startDate")]
        public string? StartDate { get; set; }

        [JsonPropertyName("endDate")]
        public string? EndDate { get; set; }

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

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updateSw")]
        public string? UpdateSw { get; set; }

        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }
    }

    /// <summary>exemptions.travelingOutside[] — persisted to the new MWRP_CE_TRAVELING_OUTSIDE /
    /// MWRP_CE_CARE_RECEIVED_DETAILS tables.</summary>
    public class TravelingOutsideDTO
    {
        [JsonPropertyName("travelingOutSideCommunityForExtendedPeriod")]
        public bool? TravelingOutsideCommunity { get; set; }

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

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updateSw")]
        public string? UpdateSw { get; set; }

        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }
    }

    /// <summary>exemptions.substanceUseDisorderTreatment[].careReceivedDetails[] — persisted to the new
    /// MWRP_CE_TREATMENT_DETAILS table.</summary>
    public class TreatmentDetailDTO
    {
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

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updateSw")]
        public string? UpdateSw { get; set; }

        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }
    }

    /// <summary>exemptions.disabilityDetails[] — persisted to the new MWRP_CE_DISABILITY_DETAILS table.</summary>
    public class DisabilityDetailDTO
    {
        [JsonPropertyName("typeOfDisability")]
        public string? TypeOfDisability { get; set; }

        [JsonPropertyName("maVerification")]
        public string? MaVerification { get; set; }

        [JsonPropertyName("maDisabilityBeginDate")]
        public string? MaDisabilityBeginDate { get; set; }

        /// <summary>Misspelling ("disbility") is exactly as sent in docs/SamplePayload.json.</summary>
        [JsonPropertyName("disbilityOutcomeDeterminedByDDS")]
        public string? DisabilityOutcomeDeterminedByDds { get; set; }

        [JsonPropertyName("updateSw")]
        public string? UpdateSw { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }
    }

    /// <summary>exemptions.livingArrangementDetails[] — persisted to the new
    /// MWRP_CE_LIVING_ARRANGEMENT_DETAILS table.</summary>
    public class LivingArrangementDetailDTO
    {
        [JsonPropertyName("livingArrangementType")]
        public string? LivingArrangementType { get; set; }

        [JsonPropertyName("effectiveBeginDate")]
        public string? EffectiveBeginDate { get; set; }

        [JsonPropertyName("effectiveEndDate")]
        public string? EffectiveEndDate { get; set; }

        [JsonPropertyName("updateSw")]
        public string? UpdateSw { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }
    }

    /// <summary>exemptions.circumstances[] / exemptions.circumstanceForExemption[] — persisted to the
    /// new MWRP_CE_CIRCUMSTANCE / MWRP_CE_CIRCUMSTANCE_DETAIL tables.</summary>
    public class CircumstanceDTO
    {
        [JsonPropertyName("circumstanceCode")]
        public string? CircumstanceCode { get; set; }

        [JsonPropertyName("circumstanceDescription")]
        public string? CircumstanceDescription { get; set; }

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

        [JsonPropertyName("enrolledForTANF")]
        public bool? EnrolledForTanf { get; set; }

        [JsonPropertyName("approvedForSNAPAndNotExemptFromSNAPWorkRequirement")]
        public bool? ApprovedForSnapNotExempt { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updateSw")]
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
        /// Renamed from "hoursOfCaregivingPerWeekMonth" to "hoursOfCaregivingPerMonth" in docs/SamplePayload.json.
        /// Persisted to the new MWRP_CE_CAREGIVER_MONTHLY_HOURS child table.</summary>
        [JsonPropertyName("hoursOfCaregivingPerMonth")]
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

        /// <summary>New in the 2026-08 revision — persisted to MWRP_CE_FORMER_FOSTER_CARE.AID_CATEGORY_WHEN_TURNED_18
        /// (VARCHAR(30) in v1.7.2), so the aid-category code string is stored as-is.</summary>
        [JsonPropertyName("aidCategoryWhenTurned18")]
        [JsonConverter(typeof(FlexibleStringConverter))]
        public string? AidCategoryWhenTurned18 { get; set; }

        /// <summary>New in docs/SamplePayload.json — persisted to CE_FORMER_FOSTER_CARE.CLIENT_RECEIVED_FORMER_FOSTER_CARE.</summary>
        [JsonPropertyName("clientReceivedFormerFosterCare")]
        public string? ClientReceivedFormerFosterCare { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updatesw")]
        public string? UpdateSw { get; set; }

        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }

        [JsonPropertyName("applicationSource")]
        public string? ApplicationSource { get; set; }
    }

    /// <summary>Item of the "incarceratedCircumstance" array (docs/SamplePayload.json; was a single
    /// "currentlyIncarcerated" object). "toa"/"livingArrangementType"/"incarceratedLast3Months" have no
    /// corresponding column on MWRP_CE_INCARCERATED_CIRCUMSTANCE and are not modeled.</summary>
    public class IncarcerationExemptionDTO
    {
        [JsonPropertyName("circumstanceCode")]
        public string? CircumstanceCode { get; set; }

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

        /// <summary>New in docs/SamplePayload.json — persisted to CE_MEDICARE_PART_AB.OVERALL_TYPE_OF_COVERAGE.</summary>
        [JsonPropertyName("overAllTypeOfCoverage")]
        public string? OverallTypeOfCoverage { get; set; }

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

    /// <summary>Backs the "medicallyFrail" exemption (was "frailty"); now an array in docs/SamplePayload.json.</summary>
    public class FrailtyExemptionDTO
    {
        [JsonPropertyName("circumstanceCode")]
        public string? CircumstanceCode { get; set; }

        /// <summary>Sent as a boolean flag in docs/SamplePayload.json (was previously modeled as free text).</summary>
        [JsonPropertyName("seriousMedicalCondition")]
        public bool? SeriousMedicalCondition { get; set; }

        [JsonPropertyName("seriousHealthProblemImpactsYourLife")]
        public bool? SeriousHealthImpact { get; set; }

        [JsonPropertyName("mentalHealth")]
        public bool? MentalHealth { get; set; }

        [JsonPropertyName("physicalDisability")]
        public bool? PhysicalDisability { get; set; }

        [JsonPropertyName("verificationSource")]
        public string? VerificationSource { get; set; }

        /// <summary>New in docs/SamplePayload.json — persisted to the new MWRP_CE_CBC_TYPE_DETAILS table.</summary>
        [JsonPropertyName("cBCTypeDetails")]
        public List<CbcTypeDetailDTO>? CbcTypeDetails { get; set; }

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

        /// <summary>New in docs/SamplePayload.json — persisted to CE_FRAILTY.LTSS_SCREENING_IND.</summary>
        [JsonPropertyName("lTSSScreening")]
        public bool? LtssScreening { get; set; }

        /// <summary>New in docs/SamplePayload.json — persisted to CE_FRAILTY.SOLQ_TITLE_II_DISABLED_IND.</summary>
        [JsonPropertyName("sOLQTitleIIDisabled")]
        public bool? SolqTitleIIDisabled { get; set; }

        /// <summary>New in docs/SamplePayload.json — persisted to CE_FRAILTY.SOLQ_TITLE_XVI_DISABLED_IND.</summary>
        [JsonPropertyName("sOLQTitleXVIDisabled")]
        public bool? SolqTitleXviDisabled { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updatesw")]
        public string? UpdateSw { get; set; }

        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }

        [JsonPropertyName("applicationSource")]
        public string? ApplicationSource { get; set; }
    }

    /// <summary>exemptions.medicallyFrail[].cBCTypeDetails[] — persisted to the new
    /// MWRP_CE_CBC_TYPE_DETAILS table.</summary>
    public class CbcTypeDetailDTO
    {
        [JsonPropertyName("waiverSw")]
        public string? WaiverSw { get; set; }

        [JsonPropertyName("paceCbcType")]
        public string? PaceCbcType { get; set; }

        [JsonPropertyName("cbcType")]
        public string? CbcType { get; set; }

        [JsonPropertyName("maBeginDate")]
        public string? MaBeginDate { get; set; }

        [JsonPropertyName("maEndDate")]
        public string? MaEndDate { get; set; }

        [JsonPropertyName("updateSw")]
        public string? UpdateSw { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }
    }

    /// <summary>New in the 2026-08 revision — persisted to the new MWRP_CE_VETERAN_DISABILITY_CIRCUMSTANCE table.</summary>
    public class VeteranDisabilityExemptionDTO
    {
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

        /// <summary>New in docs/SamplePayload.json — persisted to CE_INCOME_DETAILS.INCOME_TYPE.</summary>
        [JsonPropertyName("incomeType")]
        public string? IncomeType { get; set; }

        [JsonPropertyName("employmentType")]
        public string? EmploymentType { get; set; }

        [JsonPropertyName("isSeasonalEmployment")]
        public bool? IsSeasonalEmployment { get; set; }

        [JsonPropertyName("seasonalEmploymentType")]
        public string? SeasonalEmploymentType { get; set; }

        /// <summary>Renamed from "isPriorEmployment" to "priorOrCurrentEmployment" in docs/SamplePayload.json.</summary>
        [JsonPropertyName("priorOrCurrentEmployment")]
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
        [JsonConverter(typeof(FlexibleStringConverter))]
        public string? Ein { get; set; }

        [JsonPropertyName("budgetBeginDt")]
        public string? BudgetBeginDate { get; set; }

        [JsonPropertyName("budgetEndDt")]
        public string? BudgetEndDate { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        /// <summary>New in docs/SamplePayload.json — persisted to CE_EV_INDV_INCOME_BUDGET.VERIFICATION_SOURCE.</summary>
        [JsonPropertyName("verificationSource")]
        public string? VerificationSource { get; set; }

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

        /// <summary>Fixes a typo ("EligParfStatusCd") — docs/SamplePayload.json sends "eligPartStatusCd".
        /// NOTE: MWRP_CE_BUDGET_GROUP_MEMBER.ELIG_PART_STATUS_CD is typed DATE in docs/Table_v17.txt, but
        /// the payload sends a status code string (e.g. "ACTIVE"), which cannot be parsed as a date. This
        /// value will not persist until the column type or the payload contract is reconciled.</summary>
        [JsonPropertyName("eligPartStatusCd")]
        public string? EligPartStatusCd { get; set; }
    }

    // ─── Job Training / In-Kind or Unpaid Work (CE_JOB_TRAINING / CE_INKIND_UNPAID_WORK) ─────

    public class JobTrainingEntryDTO
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

        [JsonPropertyName("verificationSource")]
        public string? VerificationSource { get; set; }

        [JsonPropertyName("programName")]
        public string? ProgramName { get; set; }

        /// <summary>New in docs/SamplePayload.json — only present on jobTraining[] entries; persisted to
        /// MWRP_CE_JOB_TRAINING.PROGRAM_TYPE (MWRP_CE_INKIND_UNPAID_WORK has no such column).</summary>
        [JsonPropertyName("programType")]
        public string? ProgramType { get; set; }

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

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updateSw")]
        public string? UpdateSw { get; set; }

        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }

        [JsonPropertyName("applicationSource")]
        public string? ApplicationSource { get; set; }
    }

    public class CourseOfStudyDTO
    {
        [JsonPropertyName("course")]
        public string? Course { get; set; }

        [JsonPropertyName("ncesCIPCode")]
        [JsonConverter(typeof(FlexibleStringConverter))]
        public string? NcesCipCode { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("updateSw")]
        public string? UpdateSw { get; set; }

        [JsonPropertyName("vaCMSSequenceNum")]
        public string? VaCmsSequenceNumber { get; set; }

        [JsonPropertyName("applicationSource")]
        public string? ApplicationSource { get; set; }
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
