using MES_EDWS.Models;
using MES_EDWS.Options;
using Microsoft.Extensions.Options;
using Teradata.Client.Provider;

namespace MES_EDWS.Services
{
    /// <summary>
    /// Persists CEP-ICD-003 v1.7.4 CE verification result payloads to the MWRP_CE_* Teradata tables
    /// defined in docs/Version_1_7_4/CEP 003_Table_Defenitions V1.7.4.txt (the same shape is deployed
    /// to every environment's schema, selected via <see cref="TeradataOptions.Schema"/> from
    /// appsettings.{Environment}.json). The whole payload is written inside one transaction so a
    /// partially-saved request is never left behind.
    ///
    /// Every v1.7.4 data table is keyed by REQUEST_SEQ_NBR (NOT NULL) and, below MWRP_CE_REQUEST /
    /// MWRP_CE_DOCUMENT, by CLIENT_ID plus a VARCHAR(500) NOT NULL *_SEQ_NUM column. Each row gets a
    /// new GUID as its own *_SEQ_NUM, and child rows repeat their parent row's *_SEQ_NUM values.
    ///
    /// NOTE: when the controller finds required fields missing it still calls
    /// <see cref="SavePartialCeVerificationResultsAsync"/>. Without requestSequenceNumber nothing can
    /// be saved; otherwise rows whose NOT NULL key is absent are skipped (MWRP_CE_REQUEST without
    /// requestTimestamp, every CLIENT_ID-keyed table for a ceVerified[] entry without clientId).
    /// </summary>
    public class ClientInfoService : IClientInfoService
    {
        private readonly string _connectionString;
        private readonly ILogger<ClientInfoService> _logger;

        private const string DataSource = "HR1_MWRP";

        /// <summary>
        /// Teradata database/schema for this environment (HR1_DMAS_POC / HR1_DMAS_SIT /
        /// HR1_DMAS_UAT / HR1_DMAS), from Teradata:Schema in appsettings.{Environment}.json.
        /// </summary>
        private readonly string _schema;

        // ── Table names (docs/Version_1_7_4/CEP 003_Table_Defenitions V1.7.4.txt) ──
        private string RequestTable                       => $"{_schema}.MWRP_CE_REQUEST";
        private string DocumentTable                      => $"{_schema}.MWRP_CE_DOCUMENT";
        private string VerifiedTable                      => $"{_schema}.MWRP_CE_VERIFIED";
        private string EdwsFrailtyTable                   => $"{_schema}.MWRP_CE_EDWS_FRAILTY";
        private string RelationshipDetailsTable           => $"{_schema}.MWRP_CE_RELATIONSHIP_DETAILS";
        private string RuleEvaluationResultTable          => $"{_schema}.MWRP_CE_RULE_EVALUATION_RESULT";
        private string VeteranDisabilityCircumstanceTable => $"{_schema}.MWRP_CE_VETERAN_DISABILITY_CIRCUMSTANCE";
        private string CircumstanceTable                  => $"{_schema}.MWRP_CE_CIRCUMSTANCE";
        private string CircumstanceDetailTable            => $"{_schema}.MWRP_CE_CIRCUMSTANCE_DETAIL";
        private string IncarceratedCircumstanceTable      => $"{_schema}.MWRP_CE_INCARCERATED_CIRCUMSTANCE";
        private string PriorIncarcerationDetailTable      => $"{_schema}.MWRP_CE_PRIOR_INCARCERATION_DETAIL";
        private string MedicarePartAbTable                => $"{_schema}.MWRP_CE_MEDICARE_PART_AB";
        private string MedicarePartAbTplTable             => $"{_schema}.MWRP_CE_MEDICARE_PART_AB_TPL";
        private string AmericanIndianAlaskanNativeTable   => $"{_schema}.MWRP_CE_AMERICAN_INDIAN_ALASKAN_NATIVE";
        private string FrailtyTable                       => $"{_schema}.MWRP_CE_FRAILTY";
        private string SolqTitleDisabledTable             => $"{_schema}.MWRP_CE_SOLQ_TITLE_DISABLED";
        private string LtssScreeningTable                 => $"{_schema}.MWRP_CE_LTSS_SCREENING";
        private string CbcTypeDetailsTable                => $"{_schema}.MWRP_CE_CBC_TYPE_DETAILS";
        private string PregnancyTable                     => $"{_schema}.MWRP_CE_PREGNANCY";
        private string CareGiverCircumstanceTable         => $"{_schema}.MWRP_CE_CARE_GIVER_CIRCUMSTANCE";
        private string MonthlyHoursOfCareGivingTable      => $"{_schema}.MWRP_CE_MONTHLY_HOURS_OF_CARE_GIVING";
        private string FosterCareTable                    => $"{_schema}.MWRP_CE_FOSTER_CARE";
        private string FormerFosterCareTable              => $"{_schema}.MWRP_CE_FORMER_FOSTER_CARE";
        private string DisabilityDetailsTable             => $"{_schema}.MWRP_CE_DISABILITY_DETAILS";
        private string MedicalExemptionsTable             => $"{_schema}.MWRP_CE_MEDICAL_EXEMPTIONS";
        private string CareReceivedDetailsTable           => $"{_schema}.MWRP_CE_CARE_RECEIVED_DETAILS";
        private string TravelingOutsideTable              => $"{_schema}.MWRP_CE_TRAVELING_OUTSIDE";
        private string SubstanceUseDisorderTreatmentTable => $"{_schema}.MWRP_CE_SUBSTANCE_USE_DISORDER_TREATMENT";
        private string TreatmentDetailsTable              => $"{_schema}.MWRP_CE_TREATMENT_DETAILS";
        private string LivingArrangementDetailsTable      => $"{_schema}.MWRP_CE_LIVING_ARRANGEMENT_DETAILS";
        private string AidCategoryTable                   => $"{_schema}.MWRP_CE_AID_CATEGORY";
        private string PhoneTable                         => $"{_schema}.MWRP_CE_PHONE";
        private string AddressTable                       => $"{_schema}.MWRP_CE_ADDRESS";
        private string MagiIncomeBudgetTable              => $"{_schema}.MWRP_CE_MAGI_INCOME_BUDGET";
        private string EvEdgIncomeBudgetTable             => $"{_schema}.MWRP_CE_EV_EDG_INCOME_BUDGET";
        private string EvIndvIncomeBudgetTable            => $"{_schema}.MWRP_CE_EV_INDV_INCOME_BUDGET";
        private string BudgetGroupMemberTable             => $"{_schema}.MWRP_CE_BUDGET_GROUP_MEMBER";
        private string IncomeDetailsTable                 => $"{_schema}.MWRP_CE_INCOME_DETAILS";
        private string IncomeBudgetDetailsTable           => $"{_schema}.MWRP_CE_INCOME_BUDGET_DETAILS";
        private string TruvEmployerTable                  => $"{_schema}.MWRP_CE_TRUV_EMPLOYER";
        private string PayrollProviderTable               => $"{_schema}.MWRP_CE_PAYROLL_PROVIDER";
        private string EmploymentDetailTable              => $"{_schema}.MWRP_CE_EMPLOYMENT_DETAIL";
        private string EmployeeProfileTable               => $"{_schema}.MWRP_CE_EMPLOYEE_PROFILE";
        private string PayStatementTable                  => $"{_schema}.MWRP_CE_PAY_STATEMENT";
        private string AnnualIncomeTable                  => $"{_schema}.MWRP_CE_ANNUAL_INCOME";
        private string ElectronicallyVerifiedEmpTable     => $"{_schema}.MWRP_CE_ELECTRONICALLY_VERIFIED_EMPLOYMENT";
        private string JobTrainingTable                   => $"{_schema}.MWRP_CE_JOB_TRAINING";
        private string InKindUnpaidWorkTable              => $"{_schema}.MWRP_CE_INKIND_UNPAID_WORK";
        private string VolunteeringCommunityServiceTable  => $"{_schema}.MWRP_CE_VOLUNTEERING_COMMUNITY_SERVICE";
        private string EducationVerifiedTable             => $"{_schema}.MWRP_CE_EDUCATION_VERIFIED";
        private string NscEnrollmentTable                 => $"{_schema}.MWRP_CE_NSC_ENROLLMENT";
        private string NscEnrollmentDataTable             => $"{_schema}.MWRP_CE_NSC_ENROLLMENT_DATA";
        private string CoursesOfStudyTable                => $"{_schema}.MWRP_CE_COURSES_OF_STUDY";
        private string ResponseTable                      => $"{_schema}.MWRP_CE_RESPONSE";
        private string ErrorResponseTable                 => $"{_schema}.MWRP_CE_ERROR_RESPONSE";
        private string ErrorMessageTable                  => $"{_schema}.MWRP_CE_ERROR_MESSAGE";
        private string ErrorDetailTable                   => $"{_schema}.MWRP_CE_ERROR_DETAIL";
        private string InvalidFieldTable                  => $"{_schema}.MWRP_CE_INVALID_FIELD";
        private string ApiTransactionTable                => $"{_schema}.MWRP_CE_API_TRANSACTION";
        private string ApiRequestTable                    => $"{_schema}.MWRP_CE_API_REQUEST";
        private string ApiResponseTable                   => $"{_schema}.MWRP_CE_API_RESPONSE";

        /// <summary>The open connection/transaction and the request key every data row carries.</summary>
        private sealed record SaveScope(TdConnection Connection, TdTransaction Transaction, string RequestSeqNbr);

        public ClientInfoService(
            IConfiguration configuration,
            IOptions<TeradataOptions> teradataOptions,
            ILogger<ClientInfoService> logger)
        {
            _connectionString = configuration.GetConnectionString("TeradataConnection")
                ?? throw new InvalidOperationException("TeradataConnection connection string is not configured.");
            _schema = teradataOptions.Value.Schema;
            _logger = logger;
        }

        // ── Entry point ────────────────────────────────────────────────────────────

        public async Task<CepDWAckResponseDTO> SaveCeVerificationResultsAsync(CepDWRequestDTO request) =>
            await SaveAsync(request, acknowledge: true)
                ?? throw new InvalidOperationException("Acknowledgement was not produced.");

        public async Task SavePartialCeVerificationResultsAsync(CepDWRequestDTO request) =>
            await SaveAsync(request, acknowledge: false);

        private async Task<CepDWAckResponseDTO?> SaveAsync(CepDWRequestDTO request, bool acknowledge)
        {
            var requestSequenceNumber = NullIfWhiteSpace(request.RequestSequenceNumber);
            if (requestSequenceNumber == null)
            {
                _logger.LogWarning(
                    "requestSequenceNumber missing — nothing saved, every MWRP_CE_* data table requires REQUEST_SEQ_NBR. CaseNumber: {CaseNumber}",
                    request.CaseNumber);
                return null;
            }

            var ackRowId = NewId();

            await using var connection = new TdConnection(_connectionString);
            await connection.OpenAsync();

            await using var transaction = connection.BeginTransaction();
            var scope = new SaveScope(connection, transaction, requestSequenceNumber);

            try
            {
                if (NullIfWhiteSpace(request.Timestamp) != null)
                    await InsertRequestAsync(scope, request);
                else
                    _logger.LogWarning(
                        "requestTimestamp missing — MWRP_CE_REQUEST row skipped. CaseNumber: {CaseNumber}",
                        request.CaseNumber);

                var documentCount = await InsertDocumentsAsync(scope, request);

                var verifiedEntries = request.CeVerified ?? new List<CeVerifiedDTO>();
                for (var i = 0; i < verifiedEntries.Count; i++)
                {
                    var verified = verifiedEntries[i];
                    var clientId = NullIfWhiteSpace(verified?.ClientIdentificationNumber);
                    if (verified == null || clientId == null)
                    {
                        _logger.LogWarning(
                            "ceVerified[{Index}].clientId missing — rows for this entry skipped. CaseNumber: {CaseNumber}",
                            i, request.CaseNumber);
                        continue;
                    }

                    await InsertVerifiedAsync(scope, clientId, verified);
                }

                CepDWAckResponseDTO? response = null;
                if (acknowledge)
                {
                    response = new CepDWAckResponseDTO
                    {
                        RequestSequenceNumber = request.RequestSequenceNumber,
                        StateId               = request.State,
                        RequestSource         = request.RequestSource,
                        Acknowledgement       = new AcknowledgementDTO
                        {
                            Code             = "REQUEST_CREATED",
                            Status           = "SUCCESS",
                            Message          = "Request has been created successfully",
                            RequestId        = ackRowId.ToString(),
                            CreatedTimestamp = DateTime.UtcNow
                        }
                    };

                    // Persist the same acknowledgement that is returned to the caller.
                    await InsertResponseAsync(scope, ackRowId, request, response);
                }

                transaction.Commit();

                _logger.LogInformation(
                    "CE verification results saved to Teradata. Acknowledged: {Acknowledged}, AckRowId: {AckRowId}, " +
                    "RequestSequenceNumber: {RequestSequenceNumber}, Documents: {DocumentCount}, CaseNumber: {CaseNumber}",
                    acknowledge, ackRowId, requestSequenceNumber, documentCount, request.CaseNumber);

                return response;
            }
            catch (Exception ex)
            {
                try { transaction.Rollback(); }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx,
                        "Rollback failed after save error. AckRowId: {AckRowId}", ackRowId);
                }

                _logger.LogError(ex,
                    "Failed to save CE verification results. AckRowId: {AckRowId}, " +
                    "RequestSequenceNumber: {RequestSequenceNumber}, CaseNumber: {CaseNumber}",
                    ackRowId, requestSequenceNumber, request.CaseNumber);
                throw;
            }
        }

        // ── MWRP_CE_REQUEST ────────────────────────────────────────────────────────
        // REQUEST_TIMESTAMP is VARCHAR(100) in v1.7.4, so "requestTimestamp" is stored as sent.

        private Task InsertRequestAsync(SaveScope scope, CepDWRequestDTO request) =>
            InsertAsync(scope.Connection, scope.Transaction, RequestTable,
                ("REQUEST_TIMESTAMP", request.Timestamp),
                ("REQUEST_SEQ_NBR", scope.RequestSeqNbr),
                ("STATE_CD", request.State),
                ("REQUEST_SOURCE_CD", request.RequestSource),
                ("CASE_NBR", request.CaseNumber),
                ("SEND_EMAIL_FLG", ToYnChar(request.SendEmailToCustomer)),
                ("PREF_COMM_LANG_CD", request.PreferredCommunicationLanguage),
                ("APPLICATION_SOURCE", request.ApplicationSource));

        // ── MWRP_CE_DOCUMENT ───────────────────────────────────────────────────────
        // CLIENT_ID comes from each document's own (optional) clientId, not from ceVerified[].clientId.

        private async Task<int> InsertDocumentsAsync(SaveScope scope, CepDWRequestDTO request)
        {
            var documents = WithSeqNums(request.DocumentsUploadedIn)
                .Concat(WithSeqNums(request.DocumentsUploadedInMwrp))
                .ToList();

            foreach (var (document, seq) in documents)
            {
                await InsertAsync(scope.Connection, scope.Transaction, DocumentTable,
                    ("REQUEST_SEQ_NBR", scope.RequestSeqNbr),
                    ("DOCUMENT_SEQ_NUM", seq),
                    ("DOCUMENT_ID", document.DocumentId),
                    ("DOCUMENT_TYPE_CD", document.DocumentType),
                    ("DOCUMENT_SUBTYPE_CD", document.DocumentSubtype),
                    ("CLIENT_ID", NullIfWhiteSpace(document.ClientId)),
                    ("DOCUMENT_UPLOAD_DT", ToDbDateFromDateTime(document.DocumentUploadTimestamp)),
                    ("DOCUMENT_DELETED_DT", ToDbDateFromDateTime(document.DocumentDeletedTimestamp)),
                    ("SOURCE_CD", document.SourceSystem),
                    ("STATUS_CD", document.DocumentStatus));
            }

            return documents.Count;
        }

        // ── MWRP_CE_VERIFIED and everything hanging off it ─────────────────────────

        private async Task InsertVerifiedAsync(SaveScope scope, string clientId, CeVerifiedDTO verified)
        {
            await InsertClientRowAsync(scope, VerifiedTable, clientId,
                ("FIRST_NAME", verified.FirstName),
                ("LAST_NAME", verified.LastName),
                ("MIDDLE_NAME", verified.MiddleName),
                ("SUFFIX", verified.Suffix),
                ("GENDER_CD", verified.Gender),
                ("DATE_OF_BIRTH", ToDbDate(verified.DateOfBirth)),
                ("MMIS_ENROLLEE_ID", NullIfWhiteSpace(verified.MmisEnrolleeId)),
                ("SSN_NBR", verified.SocialSecurityNumber),
                ("MWR_STATUS_CD", verified.MwrStatus),
                ("MWR_STATUS_START_DT", ToDbDate(verified.MwrStartDate)),
                ("MWR_STATUS_END_DT", ToDbDate(verified.MwrEndDate)),
                ("REVIEW_MONTH_DT", ToDbDate(verified.ReviewMonth)),
                ("EXEMPT_EXCL_PENDING_SW", ToYnCharFromText(verified.ExemptOrExceptionOrExclusionPendingSw)),
                ("PREV_ELIG_AUTH_DT", ToDbDate(verified.PreviousEligibilityAuthorizationDate)),
                ("LAST_APPROVED_PERIOD_BEGIN_DT", ToDbDate(verified.LastApprovedPeriodBeginDate)),
                ("APPLICATION_DT", ToDbDate(verified.ApplicationDate)),
                ("RENEWAL_INITIATED_DT", ToDbDate(verified.RenewalInitiatedDate)),
                ("CHANGE_DT", ToDbDate(verified.ChangeDate)),
                ("CURR_ELIG_BEGIN_DT", ToDbDate(verified.CurrentEligibilityBeginDate)),
                ("APP_CHG_SUBMIT_DT", ToDbDate(verified.ApplicationChangeSubmissionDate)),
                ("CASE_ACTION_CD", verified.IndividualCaseAction),
                ("DUE_DT", ToDbDate(verified.DueDate)),
                ("EMAIL_VERIFIED_FLG", ToYnChar(verified.EmailVerifiedFlag)),
                ("EMAIL_ADDR", verified.Email),
                ("DRIVER_LICENSE_NUMBER", verified.DriverLicenseNumber),
                ("DRIVER_LICENSE_STATE", verified.DriverLicenseState),
                ("PASSPORT_NUMBER", verified.PassportNumber),
                ("PASSPORT_COUNTRY", verified.PassportCountry),
                ("NATIONALITY", verified.Nationality),
                ("MARITAL_STATUS", verified.MaritalStatus),
                ("PRIMARY_MODE_OF_COMMUNICATION", verified.PrimaryModeOfCommunication),
                ("VERIFICATION_TYPE", verified.VerificationType),
                ("ALT_EMAIL_ADDR", verified.AlternateEmail),
                ("HOH_FLG", ToYnChar(verified.HeadOfHousehold)),
                ("REQUIRES_CE_EVAL_FLG", ToYnChar(verified.RequiresCeEvaluation)),
                ("RELATIONSHIP_HOH_CD", verified.RelationshipWithHoh));

            // CLIENT_ID is NOT NULL; a relationship without its own clientId belongs to this individual.
            foreach (var (relationship, seq) in WithSeqNums(verified.RelationshipDetails))
            {
                await InsertClientRowAsync(scope, RelationshipDetailsTable,
                    NullIfWhiteSpace(relationship.ClientId) ?? clientId,
                    ("RELATION_SEQ_NUM", seq),
                    ("REVERSE_CLIENT_ID", relationship.ReverseClientId),
                    ("RELATION_TYPE", relationship.RelationType),
                    ("EFFECTIVE_BEGIN_DATE", ToDbDate(relationship.EffectiveBeginDate)),
                    ("EFFECTIVE_END_DATE", ToDbDate(relationship.EffectiveEndDate)),
                    ("UPDATE_SW", relationship.UpdateSw),
                    ("SOURCE_SYSTEM", relationship.Source),
                    ("VACMS_SEQUENCE_NUM", relationship.VaCmsSequenceNumber));
            }

            if (verified.EdwsMedicalFrailty is { } frailty)
            {
                await InsertClientRowAsync(scope, EdwsFrailtyTable, clientId,
                    ("REQUEST_ID", frailty.RequestId),
                    ("MEDICAL_FRAIL_IND", ToYnChar(frailty.MedicallyFrail)),
                    ("CIRCUM_START_DATE", ToDbDate(frailty.CircumstanceStartDate)),
                    ("CIRCUM_END_DATE", ToDbDate(frailty.CircumstanceEndDate)));
            }

            foreach (var (rule, _) in WithSeqNums(verified.RulesEvaluationResults))
            {
                await InsertClientRowAsync(scope, RuleEvaluationResultTable, clientId,
                    ("RULE_ID", rule.RuleId),
                    ("RULE_NM", rule.RuleName),
                    ("RESULT_CD", rule.Result),
                    ("ACTIVITY_TYPE", rule.ActivityType));
            }

            foreach (var (aidCategory, seq) in WithSeqNums(verified.AidCategory))
            {
                await InsertClientRowAsync(scope, AidCategoryTable, clientId,
                    ("AID_CAT_SEQ_NUM", seq),
                    ("AID_CATEGORY", aidCategory.Code),
                    ("START_DATE", ToDbDate(aidCategory.StartDate)),
                    ("END_DATE", ToDbDate(aidCategory.EndDate)),
                    ("SOURCE_SYSTEM", aidCategory.Source),
                    ("UPDATE_SW", aidCategory.UpdateSw),
                    ("VACMS_SEQUENCE_NUM", aidCategory.VaCmsSequenceNumber));
            }

            foreach (var (phone, seq) in WithSeqNums(verified.PhoneData))
            {
                await InsertClientRowAsync(scope, PhoneTable, clientId,
                    ("PHONE_SEQ_NUM", seq),
                    ("PHONE_TYPE", phone.PhoneType),
                    ("PHONE_NUMBER", phone.PhoneNumber),
                    ("SOURCE_SYSTEM", phone.Source),
                    ("UPDATE_SW", phone.UpdateSw),
                    ("VACMS_SEQUENCE_NUM", phone.VaCmsSequenceNumber));
            }

            foreach (var (address, seq) in WithSeqNums(verified.Addresses))
            {
                await InsertClientRowAsync(scope, AddressTable, clientId,
                    ("ADDRESS_SEQ_NUM", seq),
                    ("ADDRESS_TYPE", address.AddressType),
                    ("ADDRESS_LINE1", address.AddressLine1),
                    ("ADDRESS_LINE2", address.AddressLine2),
                    ("COUNTY", address.County),
                    ("CITY", address.City),
                    ("ZIP_CODE5", address.ZipCode5),
                    ("ZIP_CODE4", address.ZipCode4),
                    ("STATE_CD", address.State),
                    ("FIPS_CODE", address.FipsCode),
                    ("SOURCE_SYSTEM", address.Source),
                    ("UPDATE_SW", address.UpdateSw),
                    ("VACMS_SEQUENCE_NUM", address.VaCmsSequenceNumber));
            }

            if (verified.Exemptions != null)
                await InsertExemptionsAsync(scope, clientId, verified.Exemptions);

            if (verified.Engagements != null)
                await InsertEngagementsAsync(scope, clientId, verified.Engagements);
        }

        // ── Exemptions ─────────────────────────────────────────────────────────────

        private async Task InsertExemptionsAsync(SaveScope scope, string clientId, ExemptionsDTO exemptions)
        {
            if (exemptions.Pregnancy is { } pregnancy)
            {
                await InsertClientRowAsync(scope, PregnancyTable, clientId,
                    ("PREGNANCY_SEQ_NUM", NewSeqNum()),
                    ("CIRCUMSTANCE_CODE", pregnancy.CircumstanceCode),
                    ("EXPECTED_PREGNANCY_DUE_DATE", ToDbDate(pregnancy.ExpectedDueDate)),
                    ("ACTUAL_PREGNANCY_END_DATE", ToDbDate(pregnancy.ActualPregnancyEndDate)),
                    ("EFFECTIVE_BEGIN_DATE", ToDbDate(pregnancy.EffectiveBeginDate)),
                    ("EFFECTIVE_END_DATE", ToDbDate(pregnancy.EffectiveEndDate)),
                    ("SOURCE_SYSTEM", pregnancy.Source),
                    ("UPDATE_SW", pregnancy.UpdateSw),
                    ("VACMS_SEQUENCE_NUM", pregnancy.VaCmsSequenceNumber));
            }

            if (exemptions.AmericanIndianAlaskanNativeCircumstance is { } aian)
            {
                await InsertClientRowAsync(scope, AmericanIndianAlaskanNativeTable, clientId,
                    ("CIRCUMSTANCE_CODE", aian.CircumstanceCode),
                    ("AMERICAN_INDIAN_ALASKAN_NATIVE_IND", ToYnChar(aian.AmericanIndianAlaskanNative)),
                    ("FED_RECOGNIZED_TRIBE_IND", ToYnChar(aian.FedRecognizedTribe)),
                    ("TRIBE_NAME", aian.TribeName),
                    ("EFFECTIVE_BEGIN_DATE", ToDbDate(aian.EffectiveBeginDate)),
                    ("EFFECTIVE_END_DATE", ToDbDate(aian.EffectiveEndDate)),
                    ("SOURCE_SYSTEM", aian.Source),
                    ("UPDATE_SW", aian.UpdateSw),
                    ("VACMS_SEQUENCE_NUM", aian.VaCmsSequenceNumber));
            }

            foreach (var (caregiver, seq) in WithSeqNums(exemptions.CareGiverCircumstance))
            {
                await InsertClientRowAsync(scope, CareGiverCircumstanceTable, clientId,
                    ("CARE_GIVER_SEQ_NUM", seq),
                    ("CIRCUMSTANCE_CODE", caregiver.CircumstanceCode),
                    ("RELATIONSHIP_WITH_DEPENDENT", caregiver.RelationshipWithDependent),
                    ("DEPENDENT_DOB", ToDbDate(caregiver.DependentDob)),
                    ("DEPENDENT_LIVE_IN_SAME_HOME_IND", ToYnChar(caregiver.DependentLivesSameHome)),
                    ("DEPENDENT_DISABLED_IND", ToYnChar(caregiver.IsDependentDisabled)),
                    ("EFFECTIVE_BEGIN_DATE", ToDbDate(caregiver.EffectiveBeginDate)),
                    ("EFFECTIVE_END_DATE", ToDbDate(caregiver.EffectiveEndDate)),
                    ("SOURCE_SYSTEM", caregiver.Source),
                    ("UPDATE_SW", caregiver.UpdateSw),
                    ("VACMS_SEQUENCE_NUM", caregiver.VaCmsSequenceNumber));

                foreach (var (hours, hoursSeq) in WithSeqNums(caregiver.HoursOfCaregivingPerWeekMonth))
                {
                    await InsertClientRowAsync(scope, MonthlyHoursOfCareGivingTable, clientId,
                        ("CARE_GIVER_SEQ_NUM", seq),
                        ("MONTHLY_HOURS_SEQ_NUM", hoursSeq),
                        ("MONTH_AND_YEAR", hours.MonthAndYear),
                        ("MONTHLY_HOURS", hours.MonthlyHours));
                }
            }

            if (exemptions.FosterCare is { } fosterCare)
            {
                await InsertClientRowAsync(scope, FosterCareTable, clientId,
                    ("FOSTER_CARE_SEQ_NUM", NewSeqNum()),
                    ("CIRCUMSTANCE_CODE", fosterCare.CircumstanceCode),
                    ("RECEIVING_IVE_FOSTER_CARE_IND", ToYnChar(fosterCare.ReceivingIveFosterCare)),
                    ("IN_STATE_CUSTODY_IND", ToYnChar(fosterCare.InStateCustody)),
                    ("EFFECTIVE_BEGIN_DATE", ToDbDate(fosterCare.EffectiveBeginDate)),
                    ("EFFECTIVE_END_DATE", ToDbDate(fosterCare.EffectiveEndDate)),
                    ("SOURCE_SYSTEM", fosterCare.Source),
                    ("UPDATE_SW", fosterCare.UpdateSw),
                    ("VACMS_SEQUENCE_NUM", fosterCare.VaCmsSequenceNumber));
            }

            if (exemptions.FormerFosterCare is { } formerFosterCare)
            {
                await InsertClientRowAsync(scope, FormerFosterCareTable, clientId,
                    ("FORMER_FOSTER_SEQ_NUM", NewSeqNum()),
                    ("CIRCUMSTANCE_CODE", formerFosterCare.CircumstanceCode),
                    ("ENROLLED_MEDICAID_FOSTERCARE_AT_18_IND", ToYnChar(formerFosterCare.EnrolledMedicaidFosterCareAge18)),
                    ("EFFECTIVE_BEGIN_DATE", ToDbDate(formerFosterCare.EffectiveBeginDate)),
                    ("EFFECTIVE_END_DATE", ToDbDate(formerFosterCare.EffectiveEndDate)),
                    ("AGE_YRS", formerFosterCare.Age),
                    ("SOURCE_SYSTEM", formerFosterCare.Source),
                    ("UPDATE_SW", formerFosterCare.UpdateSw),
                    ("VACMS_SEQUENCE_NUM", formerFosterCare.VaCmsSequenceNumber),
                    ("AID_CATEGORY_WHEN_TURNED_18", formerFosterCare.AidCategoryWhenTurned18),
                    ("CLIENT_RECEIVED_FORMER_FOSTER_CARE", formerFosterCare.ClientReceivedFormerFosterCare));
            }

            foreach (var (incarceration, seq) in WithSeqNums(exemptions.IncarceratedCircumstance))
            {
                await InsertClientRowAsync(scope, IncarceratedCircumstanceTable, clientId,
                    ("INCAR_CIRCUM_SEQ_NUM", seq),
                    ("CIRCUMSTANCE_CODE", incarceration.CircumstanceCode),
                    ("AID_CATEGORY", incarceration.AidCategory),
                    ("FACILITY_TYPE", incarceration.FacilityType),
                    ("EFFECTIVE_BEGIN_DATE", ToDbDate(incarceration.EffectiveBeginDate)),
                    ("EFFECTIVE_END_DATE", ToDbDate(incarceration.EffectiveEndDate)),
                    ("RELEASE_DATE", ToDbDate(incarceration.ReleasedDate)),
                    ("SOURCE_SYSTEM", incarceration.Source),
                    ("UPDATE_SW", incarceration.UpdateSw),
                    ("VACMS_SEQUENCE_NUM", incarceration.VaCmsSequenceNumber));
            }

            foreach (var (prior, seq) in WithSeqNums(exemptions.PriorIncarcerationDetails))
            {
                await InsertClientRowAsync(scope, PriorIncarcerationDetailTable, clientId,
                    ("PRIOR_INCAR_SEQ_NUM", seq),
                    ("WAS_INCARCERATED_LAST_4_MONTHS", prior.WasIncarceratedLast4Months),
                    ("PRIOR_INCARCERATION_END_DATE", ToDbDate(prior.PriorIncarcerationEndDate)),
                    ("PRIOR_INCARCERATION_TYPE", prior.PriorIncarcerationType));
            }

            foreach (var (medicare, seq) in WithSeqNums(exemptions.ReceivingMedicaidPartAb))
            {
                await InsertClientRowAsync(scope, MedicarePartAbTable, clientId,
                    ("MEDICARE_SEQ_NUM", seq),
                    ("TYPE_OF_COVERAGE", medicare.CoverageType),
                    ("MEDICARE_TYPE_CD", medicare.EntitledToOrReceivingMedicareType),
                    ("START_DATE", ToDbDate(medicare.StartDate)),
                    ("END_DATE", ToDbDate(medicare.EndDate)),
                    ("SOURCE_SYSTEM", medicare.Source),
                    ("UPDATE_SW", medicare.UpdateSw),
                    ("VACMS_SEQUENCE_NUM", medicare.VaCmsSequenceNumber));
            }

            foreach (var (medicareTpl, seq) in WithSeqNums(exemptions.ReceivingMedicarePartAbTpl))
            {
                await InsertClientRowAsync(scope, MedicarePartAbTplTable, clientId,
                    ("MEDICARE_TPL_SEQ_NUM", seq),
                    ("TYPE_OF_COVERAGE", medicareTpl.CoverageType),
                    ("OVERALL_TYPE_OF_COVERAGE", medicareTpl.OverallTypeOfCoverage),
                    ("IS_SOLQ_IND", ToYnChar(medicareTpl.Solq)),
                    ("START_DATE", ToDbDate(medicareTpl.StartDate)),
                    ("END_DATE", ToDbDate(medicareTpl.EndDate)),
                    ("SOURCE_SYSTEM", medicareTpl.Source),
                    ("UPDATE_SW", medicareTpl.UpdateSw),
                    ("VACMS_SEQUENCE_NUM", medicareTpl.VaCmsSequenceNumber));
            }

            foreach (var (frailty, seq) in WithSeqNums(exemptions.MedicallyFrail))
            {
                await InsertClientRowAsync(scope, FrailtyTable, clientId,
                    ("FRAILTY_SEQ_NUM", seq),
                    ("SERIOUS_MEDICAL_CONDITION_IND", ToYnChar(frailty.SeriousMedicalCondition)),
                    ("SERIOUS_HEALTH_PROBLEM_IMPACTS_LIFE_IND", ToYnChar(frailty.SeriousHealthImpact)),
                    ("MENTAL_HEALTH_CONDITION_IND", ToYnChar(frailty.MentalHealth)),
                    ("PHYSICAL_DISABILITY_IND", ToYnChar(frailty.PhysicalDisability)),
                    ("VERIFICATION_SOURCE", frailty.VerificationSource),
                    ("EFFECTIVE_START_DATE", ToDbDate(frailty.EffectiveStartDate)),
                    ("EFFECTIVE_END_DATE", ToDbDate(frailty.EffectiveEndDate)),
                    ("DRUG_OR_ALCOHOL_USE_DISORDER_IND", ToYnChar(frailty.DrugOrAlcoholUseDisorder)),
                    ("FREQUENT_MEDICAL_CARE_IND", ToYnChar(frailty.FrequentMedicalCare)),
                    ("RELIED_ON_MEDICAL_EQUIPMENT_IND", ToYnChar(frailty.ReliedOnMedicalEquipment)),
                    ("BLIND_OR_LIMITED_VISION_IND", ToYnChar(frailty.BlindOrHaveLimitedVision)),
                    ("LTSS_SCREENING_IND", ToYnChar(frailty.LtssScreening)),
                    ("SOURCE_SYSTEM", frailty.Source),
                    ("UPDATE_SW", frailty.UpdateSw),
                    ("VACMS_SEQUENCE_NUM", frailty.VaCmsSequenceNumber),
                    ("SOLQ_TITLE_II_DISABLED_IND", ToYnChar(frailty.SolqTitleIIDisabled)),
                    ("SOLQ_TITLE_XVI_DISABLED_IND", ToYnChar(frailty.SolqTitleXviDisabled)));

                await InsertCbcTypeDetailsAsync(scope, clientId, seq, frailty.CbcTypeDetails);
            }

            // v1.7.4 moves CBC details up to exemptions, but FRAILTY_SEQ_NUM is still NOT NULL, so these
            // rows share one GUID per exemptions object that matches no MWRP_CE_FRAILTY row.
            await InsertCbcTypeDetailsAsync(scope, clientId, NewSeqNum(), exemptions.IndvCbcTypeDetails);

            foreach (var (solq, seq) in WithSeqNums(exemptions.SolqTitleDisability))
            {
                await InsertClientRowAsync(scope, SolqTitleDisabledTable, clientId,
                    ("SOLQ_TITLE_SEQ_NUM", seq),
                    ("SOLQ_TITLE_DISABLE_TYPE", solq.SolqTitleDisabledType),
                    ("EFF_BEGIN_DATE", ToDbDate(solq.EffectiveBeginDate)),
                    ("EFF_END_DATE", ToDbDate(solq.EffectiveEndDate)),
                    ("VACMS_SEQUENCE_NUM", solq.VaCmsSequenceNumber),
                    ("UPDATE_SW", solq.UpdateSw),
                    ("SOURCE_SYSTEM", solq.Source));
            }

            foreach (var (ltss, seq) in WithSeqNums(exemptions.LtssScreeningDetails))
            {
                await InsertClientRowAsync(scope, LtssScreeningTable, clientId,
                    ("LTSS_SCREENING_SEQ_NUM", seq),
                    ("LTSS_SCREENING_IND", ToYnChar(ltss.LtssScreening)),
                    ("EFF_BEGIN_DATE", ToDbDate(ltss.EffectiveBeginDate)),
                    ("EFF_END_DATE", ToDbDate(ltss.EffectiveEndDate)),
                    ("VACMS_SEQUENCE_NUM", ltss.VaCmsSequenceNumber),
                    ("UPDATE_SW", ltss.UpdateSw),
                    ("SOURCE_SYSTEM", ltss.Source));
            }

            foreach (var (medicalExemption, seq) in WithSeqNums(exemptions.MedicalExemptions))
            {
                await InsertClientRowAsync(scope, MedicalExemptionsTable, clientId,
                    ("MEDICAL_EXCEMPT_SEQ_NUM", seq),
                    ("RECVD_HOSPITAL_CARE_IND", ToYnChar(medicalExemption.ReceivedHospitalCare)),
                    ("EFFECTIVE_BEGIN_DATE", ToDbDate(medicalExemption.EffectiveBeginDate)),
                    ("EFFECTIVE_END_DATE", ToDbDate(medicalExemption.EffectiveEndDate)),
                    ("VERIFICATION_SOURCE", medicalExemption.VerificationSource),
                    ("VERIFICATION_DATE", ToDbDate(medicalExemption.VerificationDate)),
                    ("SOURCE_SYSTEM", medicalExemption.Source),
                    ("VACMS_SEQUENCE_NUM", medicalExemption.VaCmsSequenceNumber),
                    ("UPDATE_SW", medicalExemption.UpdateSw));

                await InsertCareReceivedDetailsAsync(scope, clientId, seq, medicalExemption.CareReceivedDetails);
            }

            foreach (var (travelingOutside, seq) in WithSeqNums(exemptions.TravelingOutside))
            {
                await InsertClientRowAsync(scope, TravelingOutsideTable, clientId,
                    ("TRAVEL_OUT_SEQ_NUM", seq),
                    ("TRAVELING_OUTSIDE_COMMUNITY_IND", ToYnChar(travelingOutside.TravelingOutsideCommunity)),
                    ("EFFECTIVE_BEGIN_DATE", ToDbDate(travelingOutside.EffectiveBeginDate)),
                    ("EFFECTIVE_END_DATE", ToDbDate(travelingOutside.EffectiveEndDate)),
                    ("VERIFICATION_SOURCE", travelingOutside.VerificationSource),
                    ("VERIFICATION_DATE", ToDbDate(travelingOutside.VerificationDate)),
                    ("SOURCE_SYSTEM", travelingOutside.Source),
                    ("VACMS_SEQUENCE_NUM", travelingOutside.VaCmsSequenceNumber),
                    ("UPDATE_SW", travelingOutside.UpdateSw));

                await InsertCareReceivedDetailsAsync(scope, clientId, seq, travelingOutside.CareReceivedDetails);
            }

            foreach (var (sud, seq) in WithSeqNums(exemptions.SubstanceUseDisorderTreatment))
            {
                await InsertClientRowAsync(scope, SubstanceUseDisorderTreatmentTable, clientId,
                    ("SUD_SEQ_NUM", seq),
                    ("SUBSTANCE_USE_DISORDER_TREATMENT_IND", ToYnChar(sud.SubstanceUseDisorderTreatmentInd)),
                    ("EFFECTIVE_BEGIN_DATE", ToDbDate(sud.EffectiveBeginDate)),
                    ("EFFECTIVE_END_DATE", ToDbDate(sud.EffectiveEndDate)),
                    ("VERIFICATION_SOURCE", sud.VerificationSource),
                    ("VERIFICATION_DATE", ToDbDate(sud.VerificationDate)),
                    ("SOURCE_SYSTEM", sud.Source),
                    ("VACMS_SEQUENCE_NUM", sud.VaCmsSequenceNumber),
                    ("UPDATE_SW", sud.UpdateSw));

                foreach (var (treatment, treatmentSeq) in WithSeqNums(sud.CareReceivedDetails))
                {
                    await InsertClientRowAsync(scope, TreatmentDetailsTable, clientId,
                        ("SUD_SEQ_NUM", seq),
                        ("TREATMENT_DTL_SEQ_NUM", treatmentSeq),
                        ("TREATMENT_START_DATE", ToDbDate(treatment.TreatmentStartDate)),
                        ("TREATMENT_END_DATE", ToDbDate(treatment.TreatmentEndDate)));
                }
            }

            foreach (var (disability, seq) in WithSeqNums(exemptions.DisabilityDetails))
            {
                await InsertClientRowAsync(scope, DisabilityDetailsTable, clientId,
                    ("DISABILITY_SEQ_NUM", seq),
                    ("TYPE_OF_DISABILITY", disability.TypeOfDisability),
                    ("MA_VERIFICATION", disability.MaVerification),
                    ("MA_DISABILITY_BEGIN_DATE", ToDbDate(disability.MaDisabilityBeginDate)),
                    ("MA_DISABILITY_END_DATE", ToDbDate(disability.MaDisabilityEndDate)),
                    ("DISABILITY_OUTCOME_DETERMINED_BY_DDS", disability.DisabilityOutcomeDeterminedByDds),
                    ("UPDATE_SW", disability.UpdateSw),
                    ("SOURCE_SYSTEM", disability.Source),
                    ("VACMS_SEQUENCE_NUM", disability.VaCmsSequenceNumber));
            }

            foreach (var (livingArrangement, seq) in WithSeqNums(exemptions.LivingArrangementDetails))
            {
                await InsertClientRowAsync(scope, LivingArrangementDetailsTable, clientId,
                    ("LIVING_ARNGMT_DTL_SEQ_NUM", seq),
                    ("LIVING_ARRANGEMENT_TYPE", livingArrangement.LivingArrangementType),
                    ("EFFECTIVE_BEGIN_DATE", ToDbDate(livingArrangement.EffectiveBeginDate)),
                    ("EFFECTIVE_END_DATE", ToDbDate(livingArrangement.EffectiveEndDate)),
                    ("UPDATE_SW", livingArrangement.UpdateSw),
                    ("SOURCE_SYSTEM", livingArrangement.Source),
                    ("VACMS_SEQUENCE_NUM", livingArrangement.VaCmsSequenceNumber));
            }

            await InsertCircumstancesAsync(scope, clientId, exemptions.CircumstanceForExemption);
            await InsertCircumstancesAsync(scope, clientId, exemptions.Circumstances);

            if (exemptions.VeteranWith100PercentDisability is { } veteran)
            {
                await InsertClientRowAsync(scope, VeteranDisabilityCircumstanceTable, clientId,
                    ("VETERAN_DIS_SEQ_NUM", NewSeqNum()),
                    ("CIRCUMSTANCE_CODE", veteran.CircumstanceCode),
                    ("VERIFICATION_SOURCE", veteran.VerificationSource),
                    ("VERIFICATION_DATE", ToDbDate(veteran.VerificationDate)),
                    ("EFFECTIVE_BEGIN_DATE", ToDbDate(veteran.EffectiveBeginDate)),
                    ("EFFECTIVE_END_DATE", ToDbDate(veteran.EffectiveEndDate)),
                    ("VETERAN_100_PERCENT_DISABILITY_IND", ToYnChar(veteran.VeteranWith100PercentDisability)),
                    ("VETERAN_DISABILITY_SUBTYPE", veteran.VeteranDisabilitySubtype),
                    ("SOURCE_SYSTEM", veteran.Source),
                    ("UPDATE_SW", veteran.UpdateSw),
                    ("VACMS_SEQUENCE_NUM", veteran.VaCmsSequenceNumber));
            }
        }

        // ── MWRP_CE_CBC_TYPE_DETAILS ───────────────────────────────────────────────

        private async Task InsertCbcTypeDetailsAsync(
            SaveScope scope, string clientId, string parentSeq, List<IndvCbcTypeDetailDTO>? cbcDetails)
        {
            foreach (var (cbc, seq) in WithSeqNums(cbcDetails))
            {
                await InsertClientRowAsync(scope, CbcTypeDetailsTable, clientId,
                    ("FRAILTY_SEQ_NUM", parentSeq),
                    ("CBC_TYPE_SEQ_NUM", seq),
                    ("WAIVER_SW", cbc.WaiverSw),
                    ("PACE_CBC_TYPE", cbc.PaceCbcType),
                    ("CBC_TYPE", cbc.CbcType),
                    ("BEGIN_DATE", ToDbDate(cbc.MaBeginDate)),
                    ("END_DATE", ToDbDate(cbc.MaEndDate)),
                    ("UPDATE_SW", cbc.UpdateSw),
                    ("SOURCE_SYSTEM", cbc.Source),
                    ("VACMS_SEQUENCE_NUM", cbc.VaCmsSequenceNumber));
            }
        }

        // ── MWRP_CE_CARE_RECEIVED_DETAILS ─────────────────────────────────────────
        // Shared child of medicalExemptions[] and travelingOutside[]; the DDL names the parent key
        // MEDICAL_EXCEMPT_SEQ_NUM for both, holding the parent row's *_SEQ_NUM.

        private async Task InsertCareReceivedDetailsAsync(
            SaveScope scope, string clientId, string parentSeq, List<CareReceivedDetailDTO>? careReceivedDetails)
        {
            foreach (var (careReceived, seq) in WithSeqNums(careReceivedDetails))
            {
                await InsertClientRowAsync(scope, CareReceivedDetailsTable, clientId,
                    ("MEDICAL_EXCEMPT_SEQ_NUM", parentSeq),
                    ("CARE_RCD_DTLS_SEQ_NUM", seq),
                    ("TYPE_OF_CARE_CD", careReceived.TypeOfCare),
                    ("TYPE_OF_CARE_DESC", careReceived.TypeOfCareDescription),
                    ("START_DATE", ToDbDate(careReceived.StartDate)),
                    ("END_DATE", ToDbDate(careReceived.EndDate)),
                    ("CARE_PROVIDER_NAME", careReceived.ProviderName),
                    ("CARE_PROVIDER_ADDRESS", careReceived.ProviderAddress));
            }
        }

        // ── MWRP_CE_CIRCUMSTANCE / MWRP_CE_CIRCUMSTANCE_DETAIL ────────────────────
        // Shared by exemptions.circumstances[] and exemptions.circumstanceForExemption[].

        private async Task InsertCircumstancesAsync(
            SaveScope scope, string clientId, List<CircumstanceDTO>? circumstances)
        {
            foreach (var (circumstance, seq) in WithSeqNums(circumstances))
            {
                await InsertClientRowAsync(scope, CircumstanceTable, clientId,
                    ("CIRCUMSTANCE_SEQ_NUM", seq),
                    ("CIRCUMSTANCE_CODE", circumstance.CircumstanceCode),
                    ("CIRCUMSTANCE_DESCRIPTION", circumstance.CircumstanceDescription));

                foreach (var (detail, detailSeq) in WithSeqNums(circumstance.CircumstanceDetails))
                {
                    await InsertClientRowAsync(scope, CircumstanceDetailTable, clientId,
                        ("CIRCUMSTANCE_SEQ_NUM", seq),
                        ("CIRCUMSTANCE_DETAIL_SEQ_NUM", detailSeq),
                        ("VERIFICATION_SOURCE", detail.VerificationSource),
                        ("VERIFICATION_DATE", ToDbDate(detail.VerificationDate)),
                        ("EFFECTIVE_BEGIN_DATE", ToDbDate(detail.EffectiveBeginDate)),
                        ("EFFECTIVE_END_DATE", ToDbDate(detail.EffectiveEndDate)),
                        ("BLIND_OR_DISABLED_IND", ToYnChar(detail.BlindOrDisabled)),
                        ("COMPLY_WITH_TANF_WORK_IND", ToYnChar(detail.EnrolledForTanf)),
                        ("APPROVED_FOR_SNAP_NOT_EXEMPT_IND", ToYnChar(detail.ApprovedForSnapNotExempt)),
                        ("SNAP_EXEMPT_TYPE", detail.SnapExemptionType),
                        ("SOURCE_SYSTEM", detail.Source),
                        ("UPDATE_SW", detail.UpdateSw),
                        ("VACMS_SEQUENCE_NUM", detail.VaCmsSequenceNumber));
                }
            }
        }

        // ── Engagements: income, job training, education, volunteering, budgets ────

        private async Task InsertEngagementsAsync(SaveScope scope, string clientId, EngagementsDTO engagements)
        {
            foreach (var (income, seq) in WithSeqNums(engagements.Employment))
                await InsertIncomeDetailsAsync(scope, clientId, seq, income);

            foreach (var (jobTraining, seq) in WithSeqNums(engagements.JobTraining))
                await InsertActivityAsync(scope, clientId, JobTrainingTable, "JOB_TRAIN_SEQ_NUM", seq, jobTraining);

            foreach (var (inKind, seq) in WithSeqNums(engagements.InKindOrUnpaidWork))
                await InsertActivityAsync(scope, clientId, InKindUnpaidWorkTable, "INKIND_UNPAID_SEQ_NUM", seq, inKind);

            foreach (var (education, seq) in WithSeqNums(engagements.Education))
                await InsertEducationAsync(scope, clientId, seq, education);

            foreach (var (volunteering, seq) in WithSeqNums(engagements.VolunteeringCommunityService))
            {
                await InsertClientRowAsync(scope, VolunteeringCommunityServiceTable, clientId,
                    ("VOL_COM_SRVC_SEQ_NUM", seq),
                    ("ORGANIZATION_ID", volunteering.OrganizationId),
                    ("ORGANIZATION_NAME", volunteering.OrganizationName),
                    ("MONTHLY_HOURS", volunteering.MonthlyHours),
                    ("START_DATE", ToDbDate(volunteering.StartDate)),
                    ("END_DATE", ToDbDate(volunteering.EndDate)),
                    ("VERIFICATION_SOURCE", volunteering.VerificationSource),
                    ("VERIFICATION_DATE", ToDbDate(volunteering.VerificationDate)),
                    ("SOURCE_SYSTEM", volunteering.Source),
                    ("UPDATE_SW", volunteering.UpdateSw),
                    ("VACMS_SEQUENCE_NUM", volunteering.VaCmsSequenceNumber));
            }

            foreach (var (summary, seq) in WithSeqNums(engagements.ElectronicallyVerifiedEmployment))
            {
                await InsertClientRowAsync(scope, ElectronicallyVerifiedEmpTable, clientId,
                    ("ELEC_VRFY_EMP_SEQ_NUM", seq),
                    ("VERIFICATION_SOURCE", summary.VerificationSource),
                    ("MONTHLY_INCOME", summary.MonthlyIncome),
                    ("MONTHLY_HOURS", summary.MonthlyHours),
                    ("INCOME_MONTH", summary.IncomeMonth),
                    ("INCOME_YEAR", summary.IncomeYear));
            }

            // CLIENT_ID is the budget group member's own (required) clientId; entries without one are skipped.
            foreach (var (budget, seq) in WithSeqNums(engagements.EvIndvIncomeBudget))
            {
                if (budget.ClientId is not { } budgetClientId)
                    continue;

                await InsertClientRowAsync(scope, EvIndvIncomeBudgetTable, budgetClientId.ToString(),
                    ("INDV_INCOME_BDGT_SEQ_NUM", seq),
                    ("EIN", budget.Ein),
                    ("BUDGET_BEGIN_DT", ToDbDate(budget.BudgetBeginDate)),
                    ("BUDGET_END_DT", ToDbDate(budget.BudgetEndDate)),
                    ("SOURCE_SYSTEM", budget.Source),
                    ("VERIFICATION_SOURCE", budget.VerificationSource),
                    ("TOTAL_BUDGET_AMOUNT", budget.TotalBudgetAmount),
                    ("TOTAL_BUDGET_HOURS", budget.TotalBudgetHours));
            }

            await InsertEdgIncomeBudgetAsync(scope, clientId, EvEdgIncomeBudgetTable, "INCOME_BDGT_SEQ_NUM",
                WithSeqNums(engagements.EvEdgIncomeBudget));

            await InsertEdgIncomeBudgetAsync(scope, clientId, MagiIncomeBudgetTable, "MAGI_INCOME_BDGT_SEQ_NUM",
                WithSeqNums(engagements.MagiIncomeBudget));

            foreach (var (member, seq) in WithSeqNums(engagements.BudgetGroupMembers))
            {
                if (member.ClientId is not { } memberClientId)
                    continue;

                await InsertClientRowAsync(scope, BudgetGroupMemberTable, memberClientId.ToString(),
                    ("BDGT_GRP_SEQ_NUM", seq),
                    ("EDG_NUM", member.EdgNum),
                    ("ELIG_PART_STATUS_CD", member.EligPartStatusCd));
            }
        }

        // ── MWRP_CE_EV_EDG_INCOME_BUDGET / MWRP_CE_MAGI_INCOME_BUDGET (same shape, different key name) ─

        private async Task InsertEdgIncomeBudgetAsync(
            SaveScope scope, string clientId, string table, string seqColumn,
            IEnumerable<(EdgIncomeBudgetDTO Item, string SeqNum)> budgets)
        {
            foreach (var (budget, seq) in budgets)
            {
                await InsertClientRowAsync(scope, table, clientId,
                    (seqColumn, seq),
                    ("EDG_NUM", budget.EdgNum),
                    ("ELIG_BEGIN_DT", ToDbDate(budget.EligBeginDate)),
                    ("ELIG_END_DT", ToDbDate(budget.EligEndDate)),
                    ("TOTAL_BUDGET_AMOUNT", budget.TotalBudgetAmount));
            }
        }

        // ── MWRP_CE_JOB_TRAINING / MWRP_CE_INKIND_UNPAID_WORK ─────────────────────
        // Identical shape apart from PROGRAM_TYPE, which only exists on JOB_TRAINING.

        private Task InsertActivityAsync(
            SaveScope scope, string clientId, string table, string seqColumn, string seq, InKindOrUnpaidWorkEntryDTO entry)
        {
            var columns = new List<(string Column, object? Value)>
            {
                (seqColumn, seq),
                ("ORGANIZATION_ID", entry.OrganizationId),
                ("ORGANIZATION_NAME", entry.OrganizationName),
                ("MONTHLY_HOURS", entry.MonthlyHours),
                ("START_DATE", ToDbDate(entry.StartDate)),
                ("END_DATE", ToDbDate(entry.EndDate)),
                ("VERIFICATION_DATE", ToDbDate(entry.VerificationDate)),
                ("VERIFICATION_SOURCE", entry.VerificationSource),
                ("PROGRAM_NAME", entry.ProgramName),
                ("SOURCE_SYSTEM", entry.Source),
                ("UPDATE_SW", entry.UpdateSw),
                ("VACMS_SEQUENCE_NUM", entry.VaCmsSequenceNumber),
            };

            if (entry is JobTrainingEntryDTO jobTraining)
                columns.Add(("PROGRAM_TYPE", jobTraining.ProgramType));

            return InsertClientRowAsync(scope, table, clientId, columns.ToArray());
        }

        // ── MWRP_CE_INCOME_DETAILS / MWRP_CE_INCOME_BUDGET_DETAILS / nested Truv tree ─
        // MWRP_CE_TRUV_EMPLOYER and below carry INCOME_DTLS_SEQ_NUM and TRUV_EMPLR_SEQ_NUM; the Truv
        // employment children additionally carry EMP_DTL_SEQ_NUM.

        private async Task InsertIncomeDetailsAsync(SaveScope scope, string clientId, string seq, EmploymentEntryDTO income)
        {
            await InsertClientRowAsync(scope, IncomeDetailsTable, clientId,
                ("INCOME_DTLS_SEQ_NUM", seq),
                ("EMPLOYER_NAME", income.EmployerName),
                ("EMPLOYER_EIN", income.EmployerEin),
                ("INCOME_TYPE", income.IncomeType),
                ("EMPLOYMENT_TYPE", income.EmploymentType),
                ("UNEMPLOYMENT_TYPE", income.UnemploymentType),
                ("SEASONAL_EMPLOYMENT_TYPE", income.SeasonalEmploymentType),
                ("PRIOR_OR_CURRENT_EMPLOYMENT_IND", ToYnChar(income.IsPriorEmployment)),
                ("START_DATE", ToDbDate(income.StartDate)),
                ("END_DATE", ToDbDate(income.EndDate)),
                ("SOURCE_SYSTEM", income.Source),
                ("UPDATE_SW", income.UpdateSw),
                ("VACMS_SEQUENCE_NUM", income.VaCmsSequenceNumber));

            foreach (var (budget, budgetSeq) in WithSeqNums(income.IncomeBudgetDetails))
            {
                await InsertClientRowAsync(scope, IncomeBudgetDetailsTable, clientId,
                    ("INCOME_DTLS_SEQ_NUM", seq),
                    ("INCOME_BDGT_DTLS_SEQ_NUM", budgetSeq),
                    ("MONTHLY_INCOME", budget.MonthlyIncome),
                    ("MONTHLY_HOURS", budget.MonthlyHours),
                    ("INCOME_MONTH", budget.IncomeMonth),
                    ("INCOME_YEAR", budget.IncomeYear),
                    ("VERIFICATION_SOURCE", budget.VerificationSource),
                    ("SOURCE_SYSTEM", budget.Source),
                    ("UPDATE_SW", budget.UpdateSw),
                    ("VACMS_SEQUENCE_NUM", budget.VaCmsSequenceNumber));
            }

            foreach (var (truvEmployer, truvSeq) in WithSeqNums(income.ElectronicallyVerifiedData))
            {
                await InsertClientRowAsync(scope, TruvEmployerTable, clientId,
                    ("INCOME_DTLS_SEQ_NUM", seq),
                    ("TRUV_EMPLR_SEQ_NUM", truvSeq),
                    ("TRUV_EMPLOYER_ID", truvEmployer.Id),
                    ("PRODUCT_TYPE", truvEmployer.ProductType),
                    ("STATUS_CD", truvEmployer.Status),
                    ("DATA_SOURCE", truvEmployer.DataSource),
                    ("COMPANY_NAME", truvEmployer.CompanyName),
                    ("IS_SUSPICIOUS_IND", ToYnChar(truvEmployer.IsSuspicious)),
                    ("START_DATE", ToDbDate(truvEmployer.StartDate)),
                    ("END_DATE", ToDbDate(truvEmployer.EndDate)));

                if (truvEmployer.Provider is { } provider)
                {
                    await InsertClientRowAsync(scope, PayrollProviderTable, clientId,
                        ("INCOME_DTLS_SEQ_NUM", seq),
                        ("TRUV_EMPLR_SEQ_NUM", truvSeq),
                        ("PAYROLL_PROV_SEQ_NUM", NewSeqNum()),
                        ("TRUV_EMPLOYER_ID", truvEmployer.Id),
                        ("PAYROLL_PROVIDER_ID", provider.Id),
                        ("PAYROLL_PROVIDER_NAME", provider.Name),
                        ("PAYROLL_PROVIDER_LOGO_URL", provider.LogoUrl));
                }

                foreach (var (detail, empSeq) in WithSeqNums(truvEmployer.Employments))
                    await InsertTruvEmploymentAsync(scope, clientId, seq, truvSeq, empSeq, detail);
            }
        }

        private async Task InsertTruvEmploymentAsync(
            SaveScope scope, string clientId, string incomeSeq, string truvSeq, string empSeq, TruvEmploymentDetailDTO detail)
        {
            await InsertClientRowAsync(scope, EmploymentDetailTable, clientId,
                ("INCOME_DTLS_SEQ_NUM", incomeSeq),
                ("TRUV_EMPLR_SEQ_NUM", truvSeq),
                ("EMP_DTL_SEQ_NUM", empSeq),
                ("TRUV_EMPLOYMENT_ID", detail.Id),
                ("JOB_TITLE", detail.JobTitle),
                ("JOB_TYPE", detail.JobType),
                ("START_DATE", ToDbDate(detail.StartDate)),
                ("END_DATE", ToDbDate(detail.EndDate)),
                ("IS_ACTIVE_IND", ToYnChar(detail.IsActive)),
                ("ANNUAL_INCOME", detail.Income),
                ("INCOME_UNIT", detail.IncomeUnit),
                ("PAY_RATE", detail.PayRate),
                ("PAY_FREQUENCY", detail.PayFrequency));

            if (detail.Profile is { } profile)
            {
                await InsertClientRowAsync(scope, EmployeeProfileTable, clientId,
                    ("INCOME_DTLS_SEQ_NUM", incomeSeq),
                    ("TRUV_EMPLR_SEQ_NUM", truvSeq),
                    ("EMP_DTL_SEQ_NUM", empSeq),
                    ("EMP_PROFILE_SEQ_NUM", NewSeqNum()),
                    ("EMPLOYEE_ID", profile.EmployeeId),
                    ("FULL_NAME", profile.FullName),
                    ("PAYROLL_PROVIDER_LOGO_URL", profile.LogoUrl));
            }

            foreach (var (statement, statementSeq) in WithSeqNums(detail.Statement))
            {
                await InsertClientRowAsync(scope, PayStatementTable, clientId,
                    ("INCOME_DTLS_SEQ_NUM", incomeSeq),
                    ("TRUV_EMPLR_SEQ_NUM", truvSeq),
                    ("EMP_DTL_SEQ_NUM", empSeq),
                    ("PAY_STMT_SEQ_NUM", statementSeq),
                    ("TRUV_STATEMENT_ID", statement.Id),
                    ("PAY_DATE", ToDbDate(statement.PayDate)),
                    ("NET_PAY", statement.NetPay),
                    ("NET_PAY_YTD", statement.NetPayYtd),
                    ("GROSS_PAY", statement.GrossPay),
                    ("GROSS_PAY_YTD", statement.GrossPayYtd),
                    ("BONUS", statement.Bonus),
                    ("COMMISSION", statement.Commission),
                    ("HOURS_WORKED", statement.Hours),
                    ("BASIS_OF_PAY", statement.BasisOfPay),
                    ("PERIOD_START", ToDbDate(statement.PeriodStart)),
                    ("PERIOD_END", ToDbDate(statement.PeriodEnd)),
                    ("REGULAR_PAY", statement.Regular),
                    ("REGULAR_PAY_YTD", statement.RegularYtd),
                    ("OTHER_PAY_YTD", statement.OtherPayYtd),
                    ("BONUS_YTD", statement.BonusYtd),
                    ("COMMISSION_YTD", statement.CommissionYtd),
                    ("OVERTIME_PAY", statement.OverTime),
                    ("OVERTIME_YTD", statement.OverTimeYtd),
                    ("OTHER_PAY", statement.OtherPay));
            }

            foreach (var (annualIncome, annualSeq) in WithSeqNums(detail.AnnualIncomeSummary))
            {
                await InsertClientRowAsync(scope, AnnualIncomeTable, clientId,
                    ("INCOME_DTLS_SEQ_NUM", incomeSeq),
                    ("TRUV_EMPLR_SEQ_NUM", truvSeq),
                    ("EMP_DTL_SEQ_NUM", empSeq),
                    ("ANNUAL_INCOME_SEQ_NUM", annualSeq),
                    ("TRUV_ANNUAL_INCOME_ID", annualIncome.Id),
                    ("INCOME_YEAR", annualIncome.Year),
                    ("REGULAR_PAY", annualIncome.Regular),
                    ("BONUS", annualIncome.Bonus),
                    ("COMMISSION", annualIncome.Commission),
                    ("OVERTIME_PAY", annualIncome.OverTime),
                    ("OTHER_PAY", annualIncome.OtherPay),
                    ("NET_PAY", annualIncome.NetPay),
                    ("GROSS_PAY", annualIncome.GrossPay));
            }
        }

        // ── MWRP_CE_EDUCATION_VERIFIED / MWRP_CE_NSC_ENROLLMENT / MWRP_CE_NSC_ENROLLMENT_DATA /
        //    MWRP_CE_COURSES_OF_STUDY ────────────────────────────────────────────────

        private async Task InsertEducationAsync(SaveScope scope, string clientId, string eduSeq, EducationEntryDTO education)
        {
            await InsertClientRowAsync(scope, EducationVerifiedTable, clientId,
                ("EDU_VERIFY_SEQ_NUM", eduSeq),
                ("INSTITUTION_CODE", education.InstitutionCode),
                ("INSTITUTION_NAME", education.InstitutionName),
                ("INSTITUTION_TYPE", education.InstitutionType),
                ("VERIFICATION_DATE", ToDbDate(education.VerificationDate)),
                ("VERIFICATION_SOURCE", education.VerificationSource),
                ("ENROLLMENT_STATUS", education.EnrollmentStatus),
                ("CREDIT_HOURS", education.CreditHours),
                ("MONTHLY_HOURS", education.MonthlyHours),
                ("TERM_START_DATE", ToDbDate(education.TermStartDate)),
                ("TERM_END_DATE", ToDbDate(education.TermEndDate)),
                ("SOURCE_SYSTEM", education.Source),
                ("UPDATE_SW", education.UpdateSw),
                ("VACMS_SEQUENCE_NUM", education.VaCmsSequenceNumber));

            foreach (var (nsc, nscSeq) in WithSeqNums(education.ElectronicallyVerifiedData))
            {
                await InsertClientRowAsync(scope, NscEnrollmentTable, clientId,
                    ("EDU_VERIFY_SEQ_NUM", eduSeq),
                    ("NSC_ENRL_SEQ_NUM", nscSeq),
                    ("OFFICIAL_SCHOOL_NAME", nsc.OfficialSchoolName),
                    ("SCHOOL_CODE", nsc.SchoolCode),
                    ("BRANCH_CODE", nsc.BranchCode),
                    ("CURRENT_ENROLLMENT_STATUS", nsc.CurrentEnrollmentStatus));

                foreach (var (period, periodSeq) in WithSeqNums(nsc.EnrollmentData))
                {
                    await InsertClientRowAsync(scope, NscEnrollmentDataTable, clientId,
                        ("EDU_VERIFY_SEQ_NUM", eduSeq),
                        ("NSC_ENRL_SEQ_NUM", nscSeq),
                        ("NSC_ENRL_DATA_SEQ_NUM", periodSeq),
                        ("ENROLLMENT_STATUS", period.EnrollmentStatus),
                        ("TERM_BEGIN_DATE", ToDbDate(period.TermBeginDate)),
                        ("TERM_END_DATE", ToDbDate(period.TermEndDate)),
                        ("SCHOOL_CERTIFIED_ON_DATE", ToDbDate(period.SchoolCertifiedOnDate)),
                        ("ANTICIPATED_GRADUATION_DATE", ToDbDate(period.AnticipatedGraduationDate)),
                        ("SOURCE_SYSTEM", period.Source),
                        ("UPDATE_SW", period.UpdateSw),
                        ("VACMS_SEQUENCE_NUM", period.VaCmsSequenceNumber));

                    foreach (var (course, courseSeq) in WithSeqNums(period.MajorCoursesOfStudy))
                    {
                        // The DDL makes all of these NOT NULL although the ICD leaves course, ncesCIPCode and
                        // updateSw optional; skip the row rather than fail the whole request.
                        if (NullIfWhiteSpace(course.Course) == null || NullIfWhiteSpace(course.NcesCipCode) == null ||
                            NullIfWhiteSpace(course.Source) == null || NullIfWhiteSpace(course.UpdateSw) == null ||
                            NullIfWhiteSpace(course.VaCmsSequenceNumber) == null)
                        {
                            _logger.LogWarning(
                                "majorCoursesOfStudy entry skipped — MWRP_CE_COURSES_OF_STUDY requires course, ncesCIPCode, source, updateSw and vaCMSSequenceNum. RequestSequenceNumber: {RequestSequenceNumber}",
                                scope.RequestSeqNbr);
                            continue;
                        }

                        // COURE_OF_STUDY_SEQ_NUM is spelled as in the v1.7.4 DDL.
                        await InsertClientRowAsync(scope, CoursesOfStudyTable, clientId,
                            ("EDU_VERIFY_SEQ_NUM", eduSeq),
                            ("NSC_ENRL_SEQ_NUM", nscSeq),
                            ("NSC_ENRL_DATA_SEQ_NUM", periodSeq),
                            ("COURE_OF_STUDY_SEQ_NUM", courseSeq),
                            ("COURSE_NAME", course.Course),
                            ("NCES_CIP_CODE", course.NcesCipCode),
                            ("SOURCE_SYSTEM", course.Source),
                            ("UPDATE_SW", course.UpdateSw),
                            ("VACMS_SEQUENCE_NUM", course.VaCmsSequenceNumber));
                    }
                }
            }
        }

        // ── MWRP_CE_RESPONSE ───────────────────────────────────────────────────────

        private Task InsertResponseAsync(SaveScope scope, long ackRowId, CepDWRequestDTO request, CepDWAckResponseDTO response) =>
            // Persist the exact acknowledgement fields that are returned to the caller, not a
            // separately-hardcoded copy, so the API response and MWRP_CE_RESPONSE never drift apart.
            InsertAsync(scope.Connection, scope.Transaction, ResponseTable,
                ("ACK_ROW_ID", ackRowId),
                ("REQUEST_SEQ_NBR", scope.RequestSeqNbr),
                ("STATE_ID", request.State),
                ("REQUEST_SOURCE_CD", request.RequestSource),
                ("ACK_CODE", response.Acknowledgement.Code),
                ("ACK_STATUS", response.Acknowledgement.Status),
                ("ACK_MESSAGE_TXT", response.Acknowledgement.Message),
                ("MWRP_CE_REQUEST_ID", response.Acknowledgement.RequestId),
                ("CREATED_TS", response.Acknowledgement.CreatedTimestamp));

        // ── MWRP_CE_ERROR_RESPONSE / _ERROR_MESSAGE / _ERROR_DETAIL / _INVALID_FIELD ─
        // One row per CepDWResultDTO level, joined on ERROR_TS (= result.Timestamp). Each
        // MWRP_CE_INVALID_FIELD row gets ERROR_INVALID_FLD_TS = ERROR_TS + n microseconds, the
        // TIMESTAMP(6) resolution, so rows stay distinct and keep their order. Own connection and
        // transaction, so the error is recorded even when the payload save rolled back.

        public async Task SaveErrorResultAsync(CepDWResultDTO result)
        {
            var errorTs = new DateTimeOffset(DateTime.SpecifyKind(result.Timestamp, DateTimeKind.Utc));

            try
            {
                await using var connection = new TdConnection(_connectionString);
                await connection.OpenAsync();

                await using var transaction = connection.BeginTransaction();

                await InsertAsync(connection, transaction, ErrorResponseTable,
                    ("ERROR_TS", errorTs),
                    ("CORRELATION_ID", Truncate(result.CorrelationId, 64)));

                await InsertAsync(connection, transaction, ErrorMessageTable,
                    ("ERROR_MSG_TS", errorTs),
                    ("ERROR_TS", errorTs),
                    ("ERROR_CODE", result.Message.Code),
                    ("ERROR_TYPE", Truncate(result.Message.Type, 20)),
                    ("NOTIFY_IND", ToYnChar(result.Message.Notify)));

                await InsertAsync(connection, transaction, ErrorDetailTable,
                    ("ERROR_DTL_TS", errorTs),
                    ("ERROR_TS", errorTs));

                for (var i = 0; i < result.Error.InvalidFields.Count; i++)
                {
                    var field = result.Error.InvalidFields[i];
                    await InsertAsync(connection, transaction, InvalidFieldTable,
                        ("ERROR_INVALID_FLD_TS", errorTs.AddTicks((i + 1) * TimeSpan.TicksPerMicrosecond)),
                        ("ERROR_TS", errorTs),
                        ("INVALID_FIELD_PATH", Truncate(field.Field, 500)),
                        ("VALIDATION_CODE", Truncate(field.Code, 50)),
                        ("VALIDATION_MESSAGE", Truncate(field.Message, 500)),
                        ("CORRELATION_ID", Truncate(result.CorrelationId, 64)));
                }

                transaction.Commit();

                _logger.LogInformation(
                    "Error result saved to Teradata. CorrelationId: {CorrelationId}, Code: {Code}, InvalidFields: {InvalidFieldCount}",
                    result.CorrelationId, result.Message.Code, result.Error.InvalidFields.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to save error result to Teradata. CorrelationId: {CorrelationId}", result.CorrelationId);
            }
        }

        // ── MWRP_CE_API_TRANSACTION / MWRP_CE_API_REQUEST / MWRP_CE_API_RESPONSE ──
        // Own connection and transaction, so the audit survives a rollback of the payload save.
        // TRANSACTION_ID is the per-call GUID in all three tables; CORRELATION_ID / REQUEST_ID /
        // RESPONSE_ID carry the caller's requestSequenceNumber.

        public async Task SaveApiAuditAsync(ApiAudit audit)
        {
            try
            {
                await using var connection = new TdConnection(_connectionString);
                await connection.OpenAsync();

                await using var transaction = connection.BeginTransaction();

                await InsertAsync(connection, transaction, ApiTransactionTable,
                    ("TRANSACTION_ID", audit.TransactionId),
                    ("CORRELATION_ID", audit.RequestId),
                    ("API_NAME", audit.ApiName),
                    ("REQUEST_TIME", audit.RequestTime),
                    ("RESPONSE_TIME", audit.ResponseTime),
                    ("HTTP_METHOD", audit.HttpMethod),
                    ("STATUS_CODE", audit.StatusCode));

                await InsertAsync(connection, transaction, ApiRequestTable,
                    ("REQUEST_ID", audit.RequestId),
                    ("TRANSACTION_ID", audit.TransactionId),
                    ("REQUEST_JSON", NullIfWhiteSpace(audit.RequestJson)),
                    ("CREATED_TS", audit.RequestTime));

                await InsertAsync(connection, transaction, ApiResponseTable,
                    ("RESPONSE_ID", audit.RequestId),
                    ("TRANSACTION_ID", audit.TransactionId),
                    ("RESPONSE_JSON", NullIfWhiteSpace(audit.ResponseJson)),
                    ("CREATED_TS", audit.ResponseTime));

                transaction.Commit();

                _logger.LogInformation(
                    "API audit saved to Teradata. TransactionId: {TransactionId}, RequestSequenceNumber: {RequestId}, StatusCode: {StatusCode}",
                    audit.TransactionId, audit.RequestId, audit.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to save API audit to Teradata. TransactionId: {TransactionId}, RequestSequenceNumber: {RequestId}",
                    audit.TransactionId, audit.RequestId);
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────────────

        /// <summary>Inserts one row into a CLIENT_ID + REQUEST_SEQ_NBR keyed table.</summary>
        private static Task InsertClientRowAsync(
            SaveScope scope, string table, string clientId, params (string Column, object? Value)[] columns) =>
            InsertAsync(scope.Connection, scope.Transaction, table,
                [("CLIENT_ID", clientId), ("REQUEST_SEQ_NBR", scope.RequestSeqNbr), .. columns]);

        /// <summary>Inserts one row, appending the EDWS_CURRENT_IND / EDWS_DATASOURCE / EDWS_DATE_INSERT audit columns.</summary>
        private static async Task InsertAsync(
            TdConnection connection, TdTransaction transaction, string table, params (string Column, object? Value)[] columns)
        {
            var sql =
                $"INSERT INTO {table} ({string.Join(", ", columns.Select(c => c.Column))}, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                $"VALUES ({string.Join(", ", columns.Select(_ => "?"))}, 'Y', ?, ?)";

            await using var command = new TdCommand(sql, connection) { Transaction = transaction };

            foreach (var (_, value) in columns)
                command.Parameters.Add(new TdParameter { Value = value ?? DBNull.Value });
            command.Parameters.Add(new TdParameter { Value = DataSource });
            command.Parameters.Add(new TdParameter { Value = DateTime.Today });

            await command.ExecuteNonQueryAsync();
        }

        /// <summary>Non-null items of a payload array, each paired with a new *_SEQ_NUM value.</summary>
        private static IEnumerable<(T Item, string SeqNum)> WithSeqNums<T>(IEnumerable<T?>? items) where T : class
        {
            foreach (var item in items ?? Enumerable.Empty<T?>())
            {
                if (item != null)
                    yield return (item, NewSeqNum());
            }
        }

        private static string NewSeqNum() => Guid.NewGuid().ToString();

        private static string? NullIfWhiteSpace(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value;

        private static string? Truncate(string? value, int maxLength) =>
            value is { Length: > 0 } && value.Length > maxLength ? value[..maxLength] : value;

        /// <summary>
        /// Monotonically increasing BIGINT-compatible id for MWRP_CE_RESPONSE.ACK_ROW_ID. Seeded from
        /// UTC ticks so values keep increasing across app restarts.
        /// </summary>
        private static long _idSeed = DateTime.UtcNow.Ticks;
        private static long NewId() => Interlocked.Increment(ref _idSeed);

        /// <summary>Normalises a bool? payload value to a single Y/N character.</summary>
        private static object ToYnChar(bool? value)
        {
            if (value == null) return DBNull.Value;
            return value.Value ? "Y" : "N";
        }

        private static object ToDbDate(string? dateStr)
        {
            if (string.IsNullOrWhiteSpace(dateStr)) return DBNull.Value;
            if (DateTime.TryParse(dateStr, out var dt)) return dt.Date;
            return DBNull.Value;
        }

        private static object ToDbDateFromDateTime(DateTime? value) => value?.Date ?? (object)DBNull.Value;

        /// <summary>Normalises a "Y"/"YES"/"TRUE" (or "N"/"NO"/"FALSE") text payload value to a single Y/N character.</summary>
        private static object ToYnCharFromText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return DBNull.Value;
            return value.Trim().ToUpperInvariant() is "Y" or "YES" or "TRUE" ? "Y" : "N";
        }
    }
}
