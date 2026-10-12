using MES_EDWS.Models;
using Teradata.Client.Provider;

namespace MES_EDWS.Services
{
    /// <summary>
    /// Persists CEP-ICD-003 CE verification result payloads to the HR1_DMAS_POC.MWRP_CE_*
    /// Teradata tables defined in docs/table_v17_updated.txt. Rows are correlated by
    /// CLIENT_IDENTIFICATION_NBR rather than a surrogate request/verified FK chain, matching
    /// the v17 schema's flat, per-client table design. The whole payload is written inside
    /// one transaction so a partially-saved request is never left behind.
    ///
    /// NOTE: docs/table_v17_updated.txt does not declare GENERATED ALWAYS AS IDENTITY on any
    /// primary key, and the Teradata.Client.Provider ODBC driver has no reliable way to read
    /// back a server-generated identity value from a plain INSERT. Surrogate keys used only to
    /// correlate rows within a single request (e.g. TRUV_EMPLOYER_ID, EMPLOYMENT_DETAIL_ID,
    /// CARE_GIVER_ID, FRAILTY_ID, SUD_TREATMENT_ID, CIRCUMSTANCE_ID, INCARCERATION_ID) are
    /// generated in application code (see <see cref="NewId"/>) and are not persisted back
    /// onto their parent row when the v17 table has no column for them.
    ///
    /// NOTE: MWRP_CE_BUDGET_GROUP_MEMBER.ELIG_PART_STATUS_CD and MWRP_CE_CBC_TYPE_DETAILS'
    /// PACE indicator (renamed PACE_CBC_TYPE) were both widened to VARCHAR(50) so the
    /// status/code strings sent by docs/SamplePayload.json (e.g. "ACTIVE", "PACE") persist
    /// as-is; no conversion helper is applied to either value.
    ///
    /// NOTE: the v17 update to MWRP_CE_VERIFIED added LAST_APPROVED_PERIOD_BEGIN_DT,
    /// APPLICATION_DT, and CHANGE_DT (mapped from CeVerifiedDTO.LastApprovedPeriodBeginDate,
    /// ApplicationDate, and ChangeDate) and dropped CUR_ELIG_BEG_DT/PRIMARY_COMM_METHOD_CD,
    /// which are no longer referenced in the INSERT below.
    ///
    /// NOTE: docs/TableDefenitionsV1_7_2.txt (v17.1.2) superseded docs/table_v17_updated.txt and
    /// renamed several columns: MWRP_CE_VERIFIED.MWR_START_DT/MWR_END_DT -> MWR_STATUS_START_DT/
    /// MWR_STATUS_END_DT; MWRP_CE_CIRCUMSTANCE_DETAIL.ENROLLED_FOR_TANF_IND -> COMPLY_WITH_TANF_WORK_IND;
    /// MWRP_CE_CBC_TYPE_DETAILS.MA_BEGIN_DATE/MA_END_DATE -> BEGIN_DATE/END_DATE;
    /// MWRP_CE_CARE_RECEIVED_DETAILS.TYPE_OF_CARE/PROVIDER_NAME -> TYPE_OF_CARE_CD/CARE_PROVIDER_NAME
    /// (plus a new CARE_PROVIDER_ADDRESS column with no corresponding DTO field, left unpopulated);
    /// and the MWRP_CE_PROVIDER table was renamed MWRP_CE_PAYROLL_PROVIDER with
    /// PROVIDER_ID/PROVIDER_NAME/LOGO_URL -> PAYROLL_PROVIDER_ID/PAYROLL_PROVIDER_NAME/
    /// PAYROLL_PROVIDER_LOGO_URL (MWRP_CE_EMPLOYEE_PROFILE.LOGO_URL was renamed the same way).
    /// MWRP_CE_INCOME_DETAILS also gained an UNEMPLOYMENT_TYPE column with no corresponding DTO
    /// field, so it is left unpopulated.
    /// </summary>
    public class ClientInfoService : IClientInfoService
    {
        private readonly string _connectionString;
        private readonly ILogger<ClientInfoService> _logger;

        private const string DataSource = "MES-EDWS";
        //private const string Schema = "HR1_DMAS_POC";
        //private const string Schema = "HR1_DMAS_SIT";
        private const string Schema = "HR1_DMAS_UAT";



        // ── Table names (docs/Table_v17.txt) ────────────────────────────────────────
        private const string RequestTable                      = Schema + ".MWRP_CE_REQUEST";
        private const string DocumentTable                     = Schema + ".MWRP_CE_DOCUMENT";
        private const string VerifiedTable                     = Schema + ".MWRP_CE_VERIFIED";
        private const string RelationshipDetailsTable          = Schema + ".MWRP_CE_RELATIONSHIP_DETAILS";
        private const string RuleEvaluationResultTable         = Schema + ".MWRP_CE_RULE_EVALUATION_RESULT";
        private const string VeteranDisabilityCircumstanceTable = Schema + ".MWRP_CE_VETERAN_DISABILITY_CIRCUMSTANCE";
        private const string CircumstanceTable                 = Schema + ".MWRP_CE_CIRCUMSTANCE";
        private const string CircumstanceDetailTable           = Schema + ".MWRP_CE_CIRCUMSTANCE_DETAIL";
        private const string IncarceratedCircumstanceTable     = Schema + ".MWRP_CE_INCARCERATED_CIRCUMSTANCE";
        private const string PriorIncarcerationDetailTable     = Schema + ".MWRP_CE_PRIOR_INCARCERATION_DETAIL";
        private const string MedicarePartAbTable               = Schema + ".MWRP_CE_MEDICARE_PART_AB";
        private const string MedicarePartAbTplTable            = Schema + ".MWRP_CE_MEDICARE_PART_AB_TPL";
        private const string AmericanIndianAlaskanNativeTable  = Schema + ".MWRP_CE_AMERICAN_INDIAN_ALASKAN_NATIVE";
        private const string FrailtyTable                      = Schema + ".MWRP_CE_FRAILTY";
        private const string CbcTypeDetailsTable                = Schema + ".MWRP_CE_CBC_TYPE_DETAILS";
        private const string PregnancyTable                    = Schema + ".MWRP_CE_PREGNANCY";
        private const string CareGiverCircumstanceTable        = Schema + ".MWRP_CE_CARE_GIVER_CIRCUMSTANCE";
        private const string MonthlyHoursOfCareGivingTable     = Schema + ".MWRP_CE_MONTHLY_HOURS_OF_CARE_GIVING";
        private const string FosterCareTable                    = Schema + ".MWRP_CE_FOSTER_CARE";
        private const string FormerFosterCareTable             = Schema + ".MWRP_CE_FORMER_FOSTER_CARE";
        private const string DisabilityDetailsTable             = Schema + ".MWRP_CE_DISABILITY_DETAILS";
        private const string MedicalExemptionsTable             = Schema + ".MWRP_CE_MEDICAL_EXEMPTIONS";
        private const string CareReceivedDetailsTable           = Schema + ".MWRP_CE_CARE_RECEIVED_DETAILS";
        private const string TravelingOutsideTable              = Schema + ".MWRP_CE_TRAVELING_OUTSIDE";
        private const string SubstanceUseDisorderTreatmentTable = Schema + ".MWRP_CE_SUBSTANCE_USE_DISORDER_TREATMENT";
        private const string TreatmentDetailsTable              = Schema + ".MWRP_CE_TREATMENT_DETAILS";
        private const string LivingArrangementDetailsTable      = Schema + ".MWRP_CE_LIVING_ARRANGEMENT_DETAILS";
        private const string AidCategoryTable                  = Schema + ".MWRP_CE_AID_CATEGORY";
        private const string PhoneTable                        = Schema + ".MWRP_CE_PHONE";
        private const string AddressTable                      = Schema + ".MWRP_CE_ADDRESS";
        private const string MagiIncomeBudgetTable             = Schema + ".MWRP_CE_MAGI_INCOME_BUDGET";
        private const string EvEdgIncomeBudgetTable            = Schema + ".MWRP_CE_EV_EDG_INCOME_BUDGET";
        private const string EvIndvIncomeBudgetTable           = Schema + ".MWRP_CE_EV_INDV_INCOME_BUDGET";
        private const string BudgetGroupMemberTable            = Schema + ".MWRP_CE_BUDGET_GROUP_MEMBER";
        private const string IncomeBudgetDetailsTable          = Schema + ".MWRP_CE_INCOME_BUDGET_DETAILS";
        private const string IncomeDetailsTable                = Schema + ".MWRP_CE_INCOME_DETAILS";
        private const string TruvEmployerTable                 = Schema + ".MWRP_CE_TRUV_EMPLOYER";
        private const string ProviderTable                     = Schema + ".MWRP_CE_PAYROLL_PROVIDER";
        private const string EmploymentDetailTable             = Schema + ".MWRP_CE_EMPLOYMENT_DETAIL";
        private const string EmployeeProfileTable              = Schema + ".MWRP_CE_EMPLOYEE_PROFILE";
        private const string PayStatementTable                 = Schema + ".MWRP_CE_PAY_STATEMENT";
        private const string AnnualIncomeTable                 = Schema + ".MWRP_CE_ANNUAL_INCOME";
        private const string ElectronicallyVerifiedEmpTable    = Schema + ".MWRP_CE_ELECTRONICALLY_VERIFIED_EMPLOYMENT";
        private const string JobTrainingTable                  = Schema + ".MWRP_CE_JOB_TRAINING";
        private const string InKindUnpaidWorkTable             = Schema + ".MWRP_CE_INKIND_UNPAID_WORK";
        private const string VolunteeringCommunityServiceTable = Schema + ".MWRP_CE_VOLUNTEERING_COMMUNITY_SERVICE";
        private const string EducationVerifiedTable            = Schema + ".MWRP_CE_EDUCATION_VERIFIED";
        private const string NscEnrollmentTable                = Schema + ".MWRP_CE_NSC_ENROLLMENT";
        private const string NscEnrollmentDataTable            = Schema + ".MWRP_CE_NSC_ENROLLMENT_DATA";
        private const string CoursesOfStudyTable               = Schema + ".MWRP_CE_COURSES_OF_STUDY";
        private const string ResponseTable                     = Schema + ".MWRP_CE_RESPONSE";

        public ClientInfoService(IConfiguration configuration, ILogger<ClientInfoService> logger)
        {
            _connectionString = configuration.GetConnectionString("TeradataConnection")
                ?? throw new InvalidOperationException("TeradataConnection connection string is not configured.");
            _logger = logger;
        }

        // ── Entry point ────────────────────────────────────────────────────────────

        public async Task<CepDWAckResponseDTO> SaveCeVerificationResultsAsync(CepDWRequestDTO request)
        {
            if (request.CeVerified is not { Count: > 0 })
                throw new InvalidOperationException("ceVerified must contain at least one entry.");

            var primaryClientId = request.CeVerified[0].ClientIdentificationNumber
                ?? throw new InvalidOperationException("ceVerified[0].clientId is required.");
            var ackRowId = NewId();

            await using var connection = new TdConnection(_connectionString);
            await connection.OpenAsync();

            await using var transaction = connection.BeginTransaction();

            try
            {
                await InsertRequestAsync(connection, transaction, request);

                if (request.DocumentsUploadedInMwrp is { Count: > 0 } documents)
                    await InsertDocumentsAsync(connection, transaction, request.RequestSequenceNumber, documents);

                foreach (var verified in request.CeVerified)
                {
                    var clientId = verified.ClientIdentificationNumber
                        ?? throw new InvalidOperationException("ceVerified[].clientId is required.");
                    await InsertVerifiedAsync(connection, transaction, clientId, request.RequestSequenceNumber, verified);
                }

                var response = new CepDWAckResponseDTO
                {
                    RequestSequenceNumber = request.RequestSequenceNumber,
                    StateId               = request.State,
                    RequestSource         = request.RequestSource,
                    Acknowledgement       = new AcknowledgementDTO
                    {
                        Code             = "REQUEST_CREATED",
                        Status           = "SUCCESS",
                        Message          = "Request has been created successfully",
                        NvhRequestId     = ackRowId.ToString(),
                        CreatedTimestamp = DateTime.UtcNow
                    }
                };

                // Persist the same acknowledgement that is returned to the caller.
                await InsertResponseAsync(connection, transaction, ackRowId, request, response);

                transaction.Commit();

                _logger.LogInformation(
                    "CE verification results saved to Teradata. AckRowId: {AckRowId}, " +
                    "ClientIdentificationNbr: {ClientIdentificationNbr}, CaseNumber: {CaseNumber}",
                    ackRowId, primaryClientId, request.CaseNumber);

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
                    "ClientIdentificationNbr: {ClientIdentificationNbr}, CaseNumber: {CaseNumber}",
                    ackRowId, primaryClientId, request.CaseNumber);
                throw;
            }
        }

        // ── MWRP_CE_REQUEST ────────────────────────────────────────────────────────

        private static async Task InsertRequestAsync(
            TdConnection connection, TdTransaction transaction, CepDWRequestDTO request)
        {
            var sql =
                $"INSERT INTO {RequestTable} " +
                "(REQUEST_TIMESTAMP, REQUEST_SEQ_NBR, STATE_CD, REQUEST_SOURCE_CD, " +
                "CASE_NBR, SEND_EMAIL_FLG, PREF_COMM_LANG_CD, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            await ExecuteAsync(connection, transaction, sql,
                request.Timestamp,
                request.RequestSequenceNumber,
                request.State,
                request.RequestSource,
                request.CaseNumber,
                ToYnChar(request.SendEmailToCustomer),
                request.PreferredCommunicationLanguage,
                DataSource,
                DateTime.Today);
        }

        // ── MWRP_CE_DOCUMENT ───────────────────────────────────────────────────────

        private static async Task InsertDocumentsAsync(
            TdConnection connection, TdTransaction transaction, string requestSequenceNumber, List<DocumentUploadDTO> documents)
        {
            var sql =
                $"INSERT INTO {DocumentTable} " +
                "(REQUEST_SEQ_NBR, DOCUMENT_ID, DOCUMENT_TYPE_CD, DOCUMENT_SUBTYPE_CD, CLIENT_ID, " +
                "DOCUMENT_UPLOAD_DT, DOCUMENT_DELETED_DT, SOURCE_CD, STATUS_CD, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            foreach (var document in documents)
            {
                await ExecuteAsync(connection, transaction, sql,
                    requestSequenceNumber,
                    document.DocumentId,
                    document.DocumentType,
                    document.DocumentSubtype,
                    document.ClientId?.ToString(),
                    ToDbDateFromDateTime(document.DocumentUploadTimestamp),
                    ToDbDateFromDateTime(document.DocumentDeletedTimestamp),
                    document.SourceSystem,
                    document.DocumentStatus,
                    DataSource,
                    DateTime.Today);
            }
        }

        // ── MWRP_CE_VERIFIED and everything hanging off it ─────────────────────────

        private async Task InsertVerifiedAsync(
            TdConnection connection, TdTransaction transaction, string clientId, string requestSequenceNumber, CeVerifiedDTO verified)
        {
            var sql =
                $"INSERT INTO {VerifiedTable} " +
                "(REQUEST_SEQ_NBR, FIRST_NAME, LAST_NAME, MIDDLE_NAME, SUFFIX, GENDER_CD, DATE_OF_BIRTH, " +
                "CLIENT_IDENTIFICATION_NBR, MMIS_ENROLLEE_ID, SSN_NBR, MWR_STATUS_CD, " +
                "MWR_STATUS_START_DT, MWR_STATUS_END_DT, REVIEW_MONTH_DT, EXEMPT_EXCL_PENDING_SW, PREV_ELIG_AUTH_DT, " +
                "LAST_APPROVED_PERIOD_BEGIN_DT, APPLICATION_DT, RENEWAL_INITIATED_DT, CHANGE_DT, " +
                "CURR_ELIG_BEGIN_DT, APP_CHG_SUBMIT_DT, CASE_ACTION_CD, DUE_DT, " +
                "EMAIL_VERIFIED_FLG, EMAIL_ADDR, DRIVER_LICENSE_NUMBER, DRIVER_LICENSE_STATE, PASSPORT_NUMBER, " +
                "PASSPORT_COUNTRY, NATIONALITY, MARITAL_STATUS, PRIMARY_MODE_OF_COMMUNICATION, VERIFICATION_TYPE, " +
                "ALT_EMAIL_ADDR, HOH_FLG, REQUIRES_CE_EVAL_FLG, RELATIONSHIP_HOH_CD, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            await ExecuteAsync(connection, transaction, sql,
                requestSequenceNumber,
                verified.FirstName,
                verified.LastName,
                verified.MiddleName,
                verified.Suffix,
                verified.Gender,
                ToDbDate(verified.DateOfBirth),
                clientId,
                verified.MmisEnrolleeId?.ToString(),
                verified.SocialSecurityNumber,
                verified.MwrStatus,
                ToDbDate(verified.MwrStartDate),
                ToDbDate(verified.MwrEndDate),
                ToDbDate(verified.ReviewMonth),
                ToYnCharFromText(verified.ExemptOrExceptionOrExclusionPendingSw),
                ToDbDate(verified.PreviousEligibilityAuthorizationDate),
                ToDbDate(verified.LastApprovedPeriodBeginDate),
                ToDbDate(verified.ApplicationDate),
                ToDbDate(verified.RenewalInitiatedDate),
                ToDbDate(verified.ChangeDate),
                ToDbDate(verified.CurrentEligibilityBeginDate),
                ToDbDate(verified.ApplicationChangeSubmissionDate),
                verified.CaseAction,
                ToDbDate(verified.DueDate),
                ToYnChar(verified.EmailVerifiedFlag),
                verified.Email,
                verified.DriverLicenseNumber,
                verified.DriverLicenseState,
                verified.PassportNumber,
                verified.PassportCountry,
                verified.Nationality,
                verified.MaritalStatus,
                verified.PrimaryModeOfCommunication,
                verified.VerificationType,
                verified.AlternateEmail,
                ToYnChar(verified.HeadOfHousehold),
                ToYnChar(verified.RequiresCeEvaluation),
                verified.RelationshipWithHoh,
                DataSource,
                DateTime.Today);

            if (verified.RelationshipDetails is { Count: > 0 } relationshipDetails)
                await InsertRelationshipDetailsAsync(connection, transaction, clientId, relationshipDetails);

            if (verified.RulesEvaluationResults is { Count: > 0 } rules)
                await InsertRulesEvaluationResultsAsync(connection, transaction, clientId, rules);

            if (verified.AidCategory is { Count: > 0 } aidCategories)
                await InsertAidCategoriesAsync(connection, transaction, clientId, aidCategories);

            if (verified.PhoneData is { Count: > 0 } phones)
                await InsertPhonesAsync(connection, transaction, clientId, phones);

            if (verified.Addresses is { Count: > 0 } addresses)
                await InsertAddressesAsync(connection, transaction, clientId, addresses);

            if (verified.Exemptions != null)
                await InsertExemptionsAsync(connection, transaction, clientId, verified.Exemptions);

            if (verified.Engagements != null)
                await InsertEngagementsAsync(connection, transaction, clientId, verified.Engagements);
        }

        // ── MWRP_CE_RELATIONSHIP_DETAILS ──────────────────────────────────────────

        private static async Task InsertRelationshipDetailsAsync(
            TdConnection connection, TdTransaction transaction, string clientId, List<RelationshipDetailDTO> relationships)
        {
            var sql =
                $"INSERT INTO {RelationshipDetailsTable} " +
                "(CLIENT_IDENTIFICATION_NBR, REVERSE_CLIENT_ID, RELATION_TYPE, EFFECTIVE_BEGIN_DATE, " +
                "EFFECTIVE_END_DATE, UPDATE_SW, SOURCE_SYSTEM, VACMS_SEQUENCE_NUM, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            foreach (var relationship in relationships)
            {
                await ExecuteAsync(connection, transaction, sql,
                    clientId, relationship.ReverseClientId, relationship.RelationType,
                    ToDbDate(relationship.EffectiveBeginDate), ToDbDate(relationship.EffectiveEndDate),
                    relationship.UpdateSw, relationship.Source, relationship.VaCmsSequenceNumber,
                    DataSource, DateTime.Today);
            }
        }

        // ── MWRP_CE_RULE_EVALUATION_RESULT ────────────────────────────────────────

        private static async Task InsertRulesEvaluationResultsAsync(
            TdConnection connection, TdTransaction transaction, string clientId, List<RuleEvaluationResultDTO> rules)
        {
            var sql =
                $"INSERT INTO {RuleEvaluationResultTable} " +
                "(CLIENT_IDENTIFICATION_NBR, RULE_ID, RULE_NM, RESULT_CD, ACTIVITY_TYPE, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, 'Y', ?, ?)";

            foreach (var rule in rules)
            {
                await ExecuteAsync(connection, transaction, sql,
                    clientId, rule.RuleId, rule.RuleName, rule.Result, rule.ActivityType,
                    DataSource, DateTime.Today);
            }
        }

        // ── MWRP_CE_AID_CATEGORY ───────────────────────────────────────────────────

        private static async Task InsertAidCategoriesAsync(
            TdConnection connection, TdTransaction transaction, string clientId, List<AidCategoryDTO> aidCategories)
        {
            var sql =
                $"INSERT INTO {AidCategoryTable} " +
                "(CLIENT_IDENTIFICATION_NBR, AID_CATEGORY, START_DATE, END_DATE, " +
                "SOURCE_SYSTEM, UPDATE_SW, VACMS_SEQUENCE_NUM, APPLICATION_SOURCE, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            foreach (var aidCategory in aidCategories)
            {
                await ExecuteAsync(connection, transaction, sql,
                    clientId,
                    aidCategory.Code,
                    ToDbDate(aidCategory.StartDate),
                    ToDbDate(aidCategory.EndDate),
                    aidCategory.Source,
                    aidCategory.UpdateSw,
                    aidCategory.VaCmsSequenceNumber,
                    aidCategory.ApplicationSource,
                    DataSource,
                    DateTime.Today);
            }
        }

        // ── MWRP_CE_PHONE ──────────────────────────────────────────────────────────

        private static async Task InsertPhonesAsync(
            TdConnection connection, TdTransaction transaction, string clientId, List<PhoneDataDTO> phones)
        {
            var sql =
                $"INSERT INTO {PhoneTable} " +
                "(CLIENT_IDENTIFICATION_NBR, PHONE_TYPE, PHONE_NUMBER, " +
                "SOURCE_SYSTEM, UPDATE_SW, VACMS_SEQUENCE_NUM, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            foreach (var phone in phones)
            {
                await ExecuteAsync(connection, transaction, sql,
                    clientId,
                    phone.PhoneType,
                    phone.PhoneNumber,
                    phone.Source,
                    phone.UpdateSw,
                    phone.VaCmsSequenceNumber,
                    DataSource,
                    DateTime.Today);
            }
        }

        // ── MWRP_CE_ADDRESS ────────────────────────────────────────────────────────

        private static async Task InsertAddressesAsync(
            TdConnection connection, TdTransaction transaction, string clientId, List<AddressDTO> addresses)
        {
            var sql =
                $"INSERT INTO {AddressTable} " +
                "(CLIENT_IDENTIFICATION_NBR, ADDRESS_TYPE, ADDRESS_LINE1, ADDRESS_LINE2, COUNTY, CITY, " +
                "ZIP_CODE5, ZIP_CODE4, STATE_CD, FIPS_CODE, SOURCE_SYSTEM, UPDATE_SW, VACMS_SEQUENCE_NUM, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            foreach (var address in addresses)
            {
                await ExecuteAsync(connection, transaction, sql,
                    clientId,
                    address.AddressType,
                    address.AddressLine1,
                    address.AddressLine2,
                    address.County,
                    address.City,
                    address.ZipCode5,
                    address.ZipCode4,
                    address.State,
                    address.FipsCode,
                    address.Source,
                    address.UpdateSw,
                    address.VaCmsSequenceNumber,
                    DataSource,
                    DateTime.Today);
            }
        }

        // ── Exemptions: pregnancy / caregiver / former foster care / incarceration /
        //    Medicare Part A&B / frailty / veteran disability ─────────────────────

        private async Task InsertExemptionsAsync(
            TdConnection connection, TdTransaction transaction, string clientId, ExemptionsDTO exemptions)
        {
            if (exemptions.Pregnancy is { } pregnancy)
            {
                var sql =
                    $"INSERT INTO {PregnancyTable} " +
                    "(CLIENT_IDENTIFICATION_NBR, PREGNANCY_ID, CIRCUMSTANCE_CODE, EXPECTED_PREGNANCY_DUE_DATE, " +
                    "ACTUAL_PREGNANCY_END_DATE, EFFECTIVE_BEGIN_DATE, EFFECTIVE_END_DATE, SOURCE_SYSTEM, " +
                    "UPDATE_SW, VACMS_SEQUENCE_NUM, EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                    "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

                await ExecuteAsync(connection, transaction, sql,
                    clientId, NewId(), pregnancy.CircumstanceCode,
                    ToDbDate(pregnancy.ExpectedDueDate), ToDbDate(pregnancy.ActualPregnancyEndDate),
                    ToDbDate(pregnancy.EffectiveBeginDate), ToDbDate(pregnancy.EffectiveEndDate),
                    pregnancy.Source, pregnancy.UpdateSw, pregnancy.VaCmsSequenceNumber,
                    DataSource, DateTime.Today);
            }

            if (exemptions.AmericanIndianAlaskanNativeCircumstance is { } aian)
            {
                var sql =
                    $"INSERT INTO {AmericanIndianAlaskanNativeTable} " +
                    "(CLIENT_IDENTIFICATION_NBR, CIRCUMSTANCE_CODE, AMERICAN_INDIAN_ALASKAN_NATIVE_IND, " +
                    "FED_RECOGNIZED_TRIBE_IND, TRIBE_NAME, EFFECTIVE_BEGIN_DATE, EFFECTIVE_END_DATE, " +
                    "SOURCE_SYSTEM, UPDATE_SW, VACMS_SEQUENCE_NUM, EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                    "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

                await ExecuteAsync(connection, transaction, sql,
                    clientId, aian.CircumstanceCode, ToYnChar(aian.AmericanIndianAlaskanNative),
                    ToYnChar(aian.FedRecognizedTribe), aian.TribeName,
                    ToDbDate(aian.EffectiveBeginDate), ToDbDate(aian.EffectiveEndDate),
                    aian.Source, aian.UpdateSw, aian.VaCmsSequenceNumber,
                    DataSource, DateTime.Today);
            }

            if (exemptions.CareGiverCircumstance is { Count: > 0 } caregivers)
                await InsertCaregiversAsync(connection, transaction, clientId, caregivers);

            if (exemptions.FosterCare is { } fosterCare)
            {
                var sql =
                    $"INSERT INTO {FosterCareTable} " +
                    "(CLIENT_IDENTIFICATION_NBR, CIRCUMSTANCE_CODE, RECEIVING_IVE_FOSTER_CARE_IND, " +
                    "IN_STATE_CUSTODY_IND, EFFECTIVE_BEGIN_DATE, EFFECTIVE_END_DATE, " +
                    "SOURCE_SYSTEM, UPDATE_SW, VACMS_SEQUENCE_NUM, EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                    "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

                await ExecuteAsync(connection, transaction, sql,
                    clientId, fosterCare.CircumstanceCode, ToYnChar(fosterCare.ReceivingIveFosterCare),
                    ToYnChar(fosterCare.InStateCustody),
                    ToDbDate(fosterCare.EffectiveBeginDate), ToDbDate(fosterCare.EffectiveEndDate),
                    fosterCare.Source, fosterCare.UpdateSw, fosterCare.VaCmsSequenceNumber,
                    DataSource, DateTime.Today);
            }

            if (exemptions.FormerFosterCare is { } formerFosterCare)
            {
                var sql =
                    $"INSERT INTO {FormerFosterCareTable} " +
                    "(CLIENT_IDENTIFICATION_NBR, CIRCUMSTANCE_CODE, ENROLLED_MEDICAID_FOSTERCARE_AT_18_IND, " +
                    "EFFECTIVE_BEGIN_DATE, EFFECTIVE_END_DATE, AGE_YRS, SOURCE_SYSTEM, UPDATE_SW, " +
                    "VACMS_SEQUENCE_NUM, APPLICATION_SOURCE, AID_CATEGORY_WHEN_TURNED_18, CLIENT_RECEIVED_FORMER_FOSTER_CARE, " +
                    "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                    "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

                await ExecuteAsync(connection, transaction, sql,
                    clientId, formerFosterCare.CircumstanceCode,
                    ToYnChar(formerFosterCare.EnrolledMedicaidFosterCareAge18),
                    ToDbDate(formerFosterCare.EffectiveBeginDate), ToDbDate(formerFosterCare.EffectiveEndDate),
                    formerFosterCare.Age, formerFosterCare.Source, formerFosterCare.UpdateSw,
                    formerFosterCare.VaCmsSequenceNumber, formerFosterCare.ApplicationSource,
                    formerFosterCare.AidCategoryWhenTurned18, formerFosterCare.ClientReceivedFormerFosterCare,
                    DataSource, DateTime.Today);
            }

            if (exemptions.CurrentlyIncarcerated is { Count: > 0 } incarcerations)
            {
                // TOA_CD / LIVING_ARRANGEMENT_TYPE_CD / INCARCERATED_LAST_3_MONTHS_FLG have no
                // column on MWRP_CE_INCARCERATED_CIRCUMSTANCE in docs/Table_v17.txt.
                var sql =
                    $"INSERT INTO {IncarceratedCircumstanceTable} " +
                    "(CLIENT_IDENTIFICATION_NBR, CIRCUMSTANCE_CODE, AID_CATEGORY, FACILITY_TYPE, EFFECTIVE_BEGIN_DATE, " +
                    "EFFECTIVE_END_DATE, RELEASE_DATE, SOURCE_SYSTEM, UPDATE_SW, VACMS_SEQUENCE_NUM, " +
                    "APPLICATION_SOURCE, EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                    "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

                foreach (var incarceration in incarcerations)
                {
                    await ExecuteAsync(connection, transaction, sql,
                        clientId, incarceration.CircumstanceCode, incarceration.AidCategory, incarceration.FacilityType,
                        ToDbDate(incarceration.EffectiveBeginDate), ToDbDate(incarceration.EffectiveEndDate),
                        ToDbDate(incarceration.ReleasedDate),
                        incarceration.Source, incarceration.UpdateSw, incarceration.VaCmsSequenceNumber,
                        incarceration.ApplicationSource, DataSource, DateTime.Today);
                }
            }

            if (exemptions.PriorIncarcerationDetails is { Count: > 0 } priorIncarcerations)
            {
                var sql =
                    $"INSERT INTO {PriorIncarcerationDetailTable} " +
                    "(CLIENT_IDENTIFICATION_NBR, INCARCERATION_ID, WAS_INCARCERATED_LAST_4_MONTHS, " +
                    "PRIOR_INCARCERATION_END_DATE, PRIOR_INCARCERATION_TYPE, " +
                    "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                    "VALUES (?, ?, ?, ?, ?, 'Y', ?, ?)";

                foreach (var prior in priorIncarcerations)
                {
                    await ExecuteAsync(connection, transaction, sql,
                        clientId, NewId(), prior.WasIncarceratedLast4Months,
                        ToDbDate(prior.PriorIncarcerationEndDate), prior.PriorIncarcerationType,
                        DataSource, DateTime.Today);
                }
            }

            if (exemptions.ReceivingMedicaidPartAb is { Count: > 0 } medicareEntries)
            {
                var sql =
                    $"INSERT INTO {MedicarePartAbTable} " +
                    "(CLIENT_IDENTIFICATION_NBR, TYPE_OF_COVERAGE, MEDICARE_EXPENSE_TYPE, OVERALL_TYPE_OF_COVERAGE, " +
                    "IS_SOLQ_IND, START_DATE, END_DATE, SOURCE_SYSTEM, UPDATE_SW, VACMS_SEQUENCE_NUM, " +
                    "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                    "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

                foreach (var medicare in medicareEntries)
                {
                    await ExecuteAsync(connection, transaction, sql,
                        clientId, medicare.CoverageType, medicare.MedicareExpenseType, medicare.OverallTypeOfCoverage,
                        ToYnChar(medicare.Solq), ToDbDate(medicare.StartDate), ToDbDate(medicare.EndDate),
                        medicare.Source, medicare.UpdateSw, medicare.VaCmsSequenceNumber,
                        DataSource, DateTime.Today);
                }
            }

            if (exemptions.ReceivingMedicarePartAbTpl is { Count: > 0 } medicareTplEntries)
            {
                var sql =
                    $"INSERT INTO {MedicarePartAbTplTable} " +
                    "(CLIENT_IDENTIFICATION_NBR, TYPE_OF_COVERAGE, OVERALL_TYPE_OF_COVERAGE, IS_SOLQ_IND, " +
                    "START_DATE, END_DATE, SOURCE_SYSTEM, UPDATE_SW, VACMS_SEQUENCE_NUM, " +
                    "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                    "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

                foreach (var medicareTpl in medicareTplEntries)
                {
                    await ExecuteAsync(connection, transaction, sql,
                        clientId, medicareTpl.CoverageType, medicareTpl.OverallTypeOfCoverage,
                        ToYnChar(medicareTpl.Solq), ToDbDate(medicareTpl.StartDate), ToDbDate(medicareTpl.EndDate),
                        medicareTpl.Source, medicareTpl.UpdateSw, medicareTpl.VaCmsSequenceNumber,
                        DataSource, DateTime.Today);
                }
            }

            if (exemptions.MedicallyFrail is { Count: > 0 } frailtyEntries)
            {
                var frailtySql =
                    $"INSERT INTO {FrailtyTable} " +
                    "(CLIENT_IDENTIFICATION_NBR, FRAILTY_ID, SERIOUS_MEDICAL_CONDITION_IND, " +
                    "SERIOUS_HEALTH_PROBLEM_IMPACTS_LIFE_IND, MENTAL_HEALTH_CONDITION_IND, " +
                    "PHYSICAL_DISABILITY_IND, VERIFICATION_SOURCE, EFFECTIVE_START_DATE, EFFECTIVE_END_DATE, " +
                    "DRUG_OR_ALCOHOL_USE_DISORDER_IND, FREQUENT_MEDICAL_CARE_IND, " +
                    "RELIED_ON_MEDICAL_EQUIPMENT_IND, BLIND_OR_LIMITED_VISION_IND, LTSS_SCREENING_IND, " +
                    "SOLQ_TITLE_II_DISABLED_IND, SOLQ_TITLE_XVI_DISABLED_IND, SOURCE_SYSTEM, UPDATE_SW, " +
                    "VACMS_SEQUENCE_NUM, APPLICATION_SOURCE, EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                    "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

                var cbcSql =
                    $"INSERT INTO {CbcTypeDetailsTable} " +
                    "(CLIENT_IDENTIFICATION_NBR, FRAILTY_ID, WAIVER_SW, PACE_CBC_TYPE, CBC_TYPE, " +
                    "BEGIN_DATE, END_DATE, UPDATE_SW, SOURCE_SYSTEM, VACMS_SEQUENCE_NUM, " +
                    "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                    "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

                foreach (var frailty in frailtyEntries)
                {
                    var frailtyId = NewId();

                    await ExecuteAsync(connection, transaction, frailtySql,
                        clientId, frailtyId, ToYnChar(frailty.SeriousMedicalCondition),
                        ToYnChar(frailty.SeriousHealthImpact), ToYnChar(frailty.MentalHealth),
                        ToYnChar(frailty.PhysicalDisability), frailty.VerificationSource,
                        ToDbDate(frailty.EffectiveStartDate), ToDbDate(frailty.EffectiveEndDate),
                        ToYnChar(frailty.DrugOrAlcoholUseDisorder), ToYnChar(frailty.FrequentMedicalCare),
                        ToYnChar(frailty.ReliedOnMedicalEquipment), ToYnChar(frailty.BlindOrHaveLimitedVision),
                        ToYnChar(frailty.LtssScreening), ToYnChar(frailty.SolqTitleIIDisabled),
                        ToYnChar(frailty.SolqTitleXviDisabled),
                        frailty.Source, frailty.UpdateSw, frailty.VaCmsSequenceNumber, frailty.ApplicationSource,
                        DataSource, DateTime.Today);

                    // PACE_CBC_TYPE is VARCHAR(50); "paceCbcType" is persisted as-is (e.g. "PACE").
                    foreach (var cbc in frailty.CbcTypeDetails ?? Enumerable.Empty<CbcTypeDetailDTO>())
                    {
                        await ExecuteAsync(connection, transaction, cbcSql,
                            clientId, frailtyId, cbc.WaiverSw, cbc.PaceCbcType, cbc.CbcType,
                            ToDbDate(cbc.MaBeginDate), ToDbDate(cbc.MaEndDate),
                            cbc.UpdateSw, cbc.Source, cbc.VaCmsSequenceNumber,
                            DataSource, DateTime.Today);
                    }
                }
            }

            if (exemptions.MedicalExemptions is { Count: > 0 } medicalExemptions)
            {
                var sql =
                    $"INSERT INTO {MedicalExemptionsTable} " +
                    "(CLIENT_IDENTIFICATION_NBR, RECVD_HOSPITAL_CARE_IND, EFFECTIVE_BEGIN_DATE, EFFECTIVE_END_DATE, " +
                    "VERIFICATION_SOURCE, VERIFICATION_DATE, SOURCE_SYSTEM, VACMS_SEQUENCE_NUM, UPDATE_SW, " +
                    "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                    "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

                foreach (var medicalExemption in medicalExemptions)
                {
                    await ExecuteAsync(connection, transaction, sql,
                        clientId, ToYnChar(medicalExemption.ReceivedHospitalCare),
                        ToDbDate(medicalExemption.EffectiveBeginDate), ToDbDate(medicalExemption.EffectiveEndDate),
                        medicalExemption.VerificationSource, ToDbDate(medicalExemption.VerificationDate),
                        medicalExemption.Source, medicalExemption.VaCmsSequenceNumber, medicalExemption.UpdateSw,
                        DataSource, DateTime.Today);

                    if (medicalExemption.CareReceivedDetails is { Count: > 0 } careReceived)
                        await InsertCareReceivedDetailsAsync(connection, transaction, clientId, careReceived);
                }
            }

            if (exemptions.TravelingOutside is { Count: > 0 } travelingOutsideEntries)
            {
                var sql =
                    $"INSERT INTO {TravelingOutsideTable} " +
                    "(CLIENT_IDENTIFICATION_NBR, TRAVELING_OUTSIDE_COMMUNITY_IND, EFFECTIVE_BEGIN_DATE, " +
                    "EFFECTIVE_END_DATE, VERIFICATION_SOURCE, VERIFICATION_DATE, SOURCE_SYSTEM, VACMS_SEQUENCE_NUM, " +
                    "UPDATE_SW, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                    "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";

                foreach (var travelingOutside in travelingOutsideEntries)
                {
                    await ExecuteAsync(connection, transaction, sql,
                        clientId, ToYnChar(travelingOutside.TravelingOutsideCommunity),
                        ToDbDate(travelingOutside.EffectiveBeginDate), ToDbDate(travelingOutside.EffectiveEndDate),
                        travelingOutside.VerificationSource, ToDbDate(travelingOutside.VerificationDate),
                        travelingOutside.Source, travelingOutside.VaCmsSequenceNumber, travelingOutside.UpdateSw,
                        DataSource, DateTime.Today);

                    if (travelingOutside.CareReceivedDetails is { Count: > 0 } careReceived)
                        await InsertCareReceivedDetailsAsync(connection, transaction, clientId, careReceived);
                }
            }

            if (exemptions.SubstanceUseDisorderTreatment is { Count: > 0 } sudEntries)
            {
                var sql =
                    $"INSERT INTO {SubstanceUseDisorderTreatmentTable} " +
                    "(CLIENT_IDENTIFICATION_NBR, SUBSTANCE_USE_DISORDER_TREATMENT_IND, EFFECTIVE_BEGIN_DATE, " +
                    "EFFECTIVE_END_DATE, VERIFICATION_SOURCE, VERIFICATION_DATE, SOURCE_SYSTEM, VACMS_SEQUENCE_NUM, " +
                    "UPDATE_SW, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                    "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";

                var treatmentDetailSql =
                    $"INSERT INTO {TreatmentDetailsTable} " +
                    "(CLIENT_IDENTIFICATION_NBR, SUD_TREATMENT_ID, TREATMENT_START_DATE, TREATMENT_END_DATE, " +
                    "EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                    "VALUES (?, ?, ?, ?, ?, ?)";

                foreach (var sud in sudEntries)
                {
                    await ExecuteAsync(connection, transaction, sql,
                        clientId, ToYnChar(sud.SubstanceUseDisorderTreatmentInd),
                        ToDbDate(sud.EffectiveBeginDate), ToDbDate(sud.EffectiveEndDate),
                        sud.VerificationSource, ToDbDate(sud.VerificationDate),
                        sud.Source, sud.VaCmsSequenceNumber, sud.UpdateSw,
                        DataSource, DateTime.Today);

                    var sudTreatmentId = NewId();
                    foreach (var treatment in sud.CareReceivedDetails ?? Enumerable.Empty<TreatmentDetailDTO>())
                    {
                        await ExecuteAsync(connection, transaction, treatmentDetailSql,
                            clientId, sudTreatmentId, ToDbDate(treatment.TreatmentStartDate),
                            ToDbDate(treatment.TreatmentEndDate), DataSource, DateTime.Today);
                    }
                }
            }

            if (exemptions.DisabilityDetails is { Count: > 0 } disabilityDetails)
            {
                var sql =
                    $"INSERT INTO {DisabilityDetailsTable} " +
                    "(CLIENT_IDENTIFICATION_NBR, TYPE_OF_DISABILITY, MA_VERIFICATION, MA_DISABILITY_BEGIN_DATE, " +
                    "DISABILITY_OUTCOME_DETERMINED_BY_DDS, UPDATE_SW, SOURCE_SYSTEM, VACMS_SEQUENCE_NUM, " +
                    "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                    "VALUES (?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

                foreach (var disability in disabilityDetails)
                {
                    await ExecuteAsync(connection, transaction, sql,
                        clientId, disability.TypeOfDisability, disability.MaVerification,
                        ToDbDate(disability.MaDisabilityBeginDate), disability.DisabilityOutcomeDeterminedByDds,
                        disability.UpdateSw, disability.Source, disability.VaCmsSequenceNumber,
                        DataSource, DateTime.Today);
                }
            }

            if (exemptions.LivingArrangementDetails is { Count: > 0 } livingArrangementDetails)
            {
                var sql =
                    $"INSERT INTO {LivingArrangementDetailsTable} " +
                    "(CLIENT_IDENTIFICATION_NBR, LIVING_ARRANGEMENT_TYPE, EFFECTIVE_BEGIN_DATE, EFFECTIVE_END_DATE, " +
                    "UPDATE_SW, SOURCE_SYSTEM, VACMS_SEQUENCE_NUM, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                    "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)";

                foreach (var livingArrangement in livingArrangementDetails)
                {
                    await ExecuteAsync(connection, transaction, sql,
                        clientId, livingArrangement.LivingArrangementType,
                        ToDbDate(livingArrangement.EffectiveBeginDate), ToDbDate(livingArrangement.EffectiveEndDate),
                        livingArrangement.UpdateSw, livingArrangement.Source, livingArrangement.VaCmsSequenceNumber,
                        DataSource, DateTime.Today);
                }
            }

            if (exemptions.CircumstanceForExemption is { Count: > 0 } circumstancesForExemption)
                await InsertCircumstancesAsync(connection, transaction, clientId, circumstancesForExemption);

            if (exemptions.Circumstances is { Count: > 0 } circumstances)
                await InsertCircumstancesAsync(connection, transaction, clientId, circumstances);

            if (exemptions.VeteranWith100PercentDisability is { } veteran)
            {
                var sql =
                    $"INSERT INTO {VeteranDisabilityCircumstanceTable} " +
                    "(CLIENT_IDENTIFICATION_NBR, CIRCUMSTANCE_CODE, VERIFICATION_SOURCE, VERIFICATION_DATE, " +
                    "EFFECTIVE_BEGIN_DATE, EFFECTIVE_END_DATE, VETERAN_100_PERCENT_DISABILITY_IND, " +
                    "VETERAN_DISABILITY_SUBTYPE, SOURCE_SYSTEM, UPDATE_SW, VACMS_SEQUENCE_NUM, APPLICATION_SOURCE, " +
                    "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                    "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

                await ExecuteAsync(connection, transaction, sql,
                    clientId, veteran.CircumstanceCode, veteran.VerificationSource,
                    ToDbDate(veteran.VerificationDate), ToDbDate(veteran.EffectiveBeginDate),
                    ToDbDate(veteran.EffectiveEndDate), ToYnChar(veteran.VeteranWith100PercentDisability),
                    veteran.VeteranDisabilitySubtype,
                    veteran.Source, veteran.UpdateSw, veteran.VaCmsSequenceNumber, veteran.ApplicationSource,
                    DataSource, DateTime.Today);
            }
        }

        // ── MWRP_CE_CARE_RECEIVED_DETAILS ─────────────────────────────────────────
        // Shared child table for both medicalExemptions[] and travelingOutside[]; has no
        // FK back to its parent in docs/Table_v17.txt, so rows are simply scoped by client.

        private static async Task InsertCareReceivedDetailsAsync(
            TdConnection connection, TdTransaction transaction, string clientId, List<CareReceivedDetailDTO> careReceivedDetails)
        {
            var sql =
                $"INSERT INTO {CareReceivedDetailsTable} " +
                "(CLIENT_IDENTIFICATION_NBR, TYPE_OF_CARE_CD, START_DATE, END_DATE, CARE_PROVIDER_NAME, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, 'Y', ?, ?)";

            foreach (var careReceived in careReceivedDetails)
            {
                await ExecuteAsync(connection, transaction, sql,
                    clientId, careReceived.TypeOfCare, ToDbDate(careReceived.StartDate),
                    ToDbDate(careReceived.EndDate), careReceived.ProviderName,
                    DataSource, DateTime.Today);
            }
        }

        // ── MWRP_CE_CIRCUMSTANCE / MWRP_CE_CIRCUMSTANCE_DETAIL ────────────────────
        // Shared by both exemptions.circumstances[] and exemptions.circumstanceForExemption[].

        private static async Task InsertCircumstancesAsync(
            TdConnection connection, TdTransaction transaction, string clientId, List<CircumstanceDTO> circumstances)
        {
            var circumstanceSql =
                $"INSERT INTO {CircumstanceTable} " +
                "(CLIENT_IDENTIFICATION_NBR, CIRCUMSTANCE_ID, CIRCUMSTANCE_CODE, CIRCUMSTANCE_DESCRIPTION, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, 'Y', ?, ?)";

            var circumstanceDetailSql =
                $"INSERT INTO {CircumstanceDetailTable} " +
                "(CLIENT_IDENTIFICATION_NBR, CIRCUMSTANCE_ID, CIRCUMSTANCE_DETAIL_ID, VERIFICATION_SOURCE, " +
                "VERIFICATION_DATE, EFFECTIVE_BEGIN_DATE, EFFECTIVE_END_DATE, BLIND_OR_DISABLED_IND, " +
                "COMPLY_WITH_TANF_WORK_IND, APPROVED_FOR_SNAP_NOT_EXEMPT_IND, SOURCE_SYSTEM, UPDATE_SW, " +
                "VACMS_SEQUENCE_NUM, APPLICATION_SOURCE, EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            foreach (var circumstance in circumstances)
            {
                var circumstanceId = NewId();

                await ExecuteAsync(connection, transaction, circumstanceSql,
                    clientId, circumstanceId, circumstance.CircumstanceCode, circumstance.CircumstanceDescription,
                    DataSource, DateTime.Today);

                foreach (var detail in circumstance.CircumstanceDetails ?? Enumerable.Empty<CircumstanceDetailDTO>())
                {
                    await ExecuteAsync(connection, transaction, circumstanceDetailSql,
                        clientId, circumstanceId, NewId(), detail.VerificationSource,
                        ToDbDate(detail.VerificationDate), ToDbDate(detail.EffectiveBeginDate),
                        ToDbDate(detail.EffectiveEndDate), ToYnChar(detail.BlindOrDisabled),
                        ToYnChar(detail.EnrolledForTanf), ToYnChar(detail.ApprovedForSnapNotExempt),
                        detail.Source, detail.UpdateSw, detail.VaCmsSequenceNumber, detail.ApplicationSource,
                        DataSource, DateTime.Today);
                }
            }
        }

        // ── MWRP_CE_CARE_GIVER_CIRCUMSTANCE / MWRP_CE_MONTHLY_HOURS_OF_CARE_GIVING ─
        // CARE_GIVER_ID is generated only to correlate the monthly-hours child rows
        // within this request; MWRP_CE_CARE_GIVER_CIRCUMSTANCE has no ID column of its own.

        private static async Task InsertCaregiversAsync(
            TdConnection connection, TdTransaction transaction, string clientId, List<CaregiverExemptionDTO> caregivers)
        {
            var sql =
                $"INSERT INTO {CareGiverCircumstanceTable} " +
                "(CLIENT_IDENTIFICATION_NBR, CIRCUMSTANCE_CODE, RELATIONSHIP_WITH_DEPENDENT, DEPENDENT_DOB, " +
                "DEPENDENT_LIVE_IN_SAME_HOME_IND, DEPENDENT_DISABLED_IND, EFFECTIVE_BEGIN_DATE, " +
                "EFFECTIVE_END_DATE, SOURCE_SYSTEM, UPDATE_SW, VACMS_SEQUENCE_NUM, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            foreach (var caregiver in caregivers)
            {
                await ExecuteAsync(connection, transaction, sql,
                    clientId, caregiver.CircumstanceCode, caregiver.RelationshipWithDependent,
                    ToDbDate(caregiver.DependentDob), ToYnChar(caregiver.DependentLivesSameHome),
                    ToYnChar(caregiver.IsDependentDisabled),
                    ToDbDate(caregiver.EffectiveBeginDate), ToDbDate(caregiver.EffectiveEndDate),
                    caregiver.Source, caregiver.UpdateSw, caregiver.VaCmsSequenceNumber,
                    DataSource, DateTime.Today);

                if (caregiver.HoursOfCaregivingPerWeekMonth is { Count: > 0 } monthlyHours)
                    await InsertCaregiverMonthlyHoursAsync(connection, transaction, clientId, NewId(), monthlyHours);
            }
        }

        private static async Task InsertCaregiverMonthlyHoursAsync(
            TdConnection connection, TdTransaction transaction, string clientId, long careGiverId,
            List<CaregiverMonthlyHoursDTO> monthlyHours)
        {
            var sql =
                $"INSERT INTO {MonthlyHoursOfCareGivingTable} " +
                "(CLIENT_IDENTIFICATION_NBR, CARE_GIVER_ID, MONTH_AND_YEAR, MONTHLY_HOURS, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, 'Y', ?, ?)";

            foreach (var hours in monthlyHours)
            {
                await ExecuteAsync(connection, transaction, sql,
                    clientId, careGiverId, hours.MonthAndYear, hours.MonthlyHours, DataSource, DateTime.Today);
            }
        }

        // ── Engagements: employment, job training, education, volunteering, budgets ──

        private async Task InsertEngagementsAsync(
            TdConnection connection, TdTransaction transaction, string clientId, EngagementsDTO engagements)
        {
            if (engagements.Employment is { Count: > 0 } employments)
                await InsertEmploymentAsync(connection, transaction, clientId, employments);

            if (engagements.JobTraining is { Count: > 0 } jobTraining)
                await InsertJobTrainingLikeAsync(connection, transaction, JobTrainingTable, clientId, jobTraining, includeProgramType: true);

            if (engagements.InKindOrUnpaidWork is { Count: > 0 } inKind)
                await InsertJobTrainingLikeAsync(connection, transaction, InKindUnpaidWorkTable, clientId, inKind, includeProgramType: false);

            if (engagements.Education is { Count: > 0 } education)
                await InsertEducationAsync(connection, transaction, clientId, education);

            if (engagements.VolunteeringCommunityService is { Count: > 0 } volunteering)
                await InsertVolunteeringAsync(connection, transaction, clientId, volunteering);

            if (engagements.ElectronicallyVerifiedEmployment is { Count: > 0 } summaries)
                await InsertElectronicallyVerifiedEmploymentAsync(connection, transaction, clientId, summaries);

            if (engagements.EvIndvIncomeBudget is { Count: > 0 } indvBudgets)
                await InsertIndvIncomeBudgetAsync(connection, transaction, clientId, indvBudgets);

            if (engagements.EvEdgIncomeBudget is { Count: > 0 } edgBudgets)
                await InsertEdgIncomeBudgetAsync(connection, transaction, EvEdgIncomeBudgetTable, clientId, edgBudgets);

            if (engagements.MagiIncomeBudget is { Count: > 0 } magiBudgets)
                await InsertEdgIncomeBudgetAsync(connection, transaction, MagiIncomeBudgetTable, clientId, magiBudgets);

            if (engagements.BudgetGroupMembers is { Count: > 0 } groupMembers)
                await InsertBudgetGroupMembersAsync(connection, transaction, clientId, groupMembers);
        }

        // ── MWRP_CE_INCOME_DETAILS / MWRP_CE_INCOME_BUDGET_DETAILS / nested Truv tree ─
        // INCOME_DETAILS has no ID column of its own in docs/Table_v17.txt; an in-memory
        // id is generated only so the Truv employer tree below can share the same
        // CLIENT_IDENTIFICATION_NBR-scoped insert pattern as the rest of the file.

        private static async Task InsertEmploymentAsync(
            TdConnection connection, TdTransaction transaction, string clientId, List<EmploymentEntryDTO> employments)
        {
            var employmentSql =
                $"INSERT INTO {IncomeDetailsTable} " +
                "(CLIENT_IDENTIFICATION_NBR, EMPLOYER_NAME, EMPLOYER_EIN, INCOME_TYPE, EMPLOYMENT_TYPE, " +
                "SEASONAL_EMPLOYMENT_TYPE, PRIOR_OR_CURRENT_EMPLOYMENT_IND, START_DATE, END_DATE, " +
                "SOURCE_SYSTEM, UPDATE_SW, VACMS_SEQUENCE_NUM, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            var budgetSql =
                $"INSERT INTO {IncomeBudgetDetailsTable} " +
                "(CLIENT_IDENTIFICATION_NBR, INCOME_DETAIL_ID, MONTHLY_INCOME, MONTHLY_HOURS, INCOME_MONTH, " +
                "INCOME_YEAR, VERIFICATION_SOURCE, SOURCE_SYSTEM, UPDATE_SW, VACMS_SEQUENCE_NUM, " +
                "APPLICATION_SOURCE, EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            foreach (var employment in employments)
            {
                // EMPLOYMENT_ACTIVE_FLG / IN_KIND_UNPAID_FLG / SEASONAL_EMPLOYMENT_FLG have no
                // column on MWRP_CE_INCOME_DETAILS in docs/Table_v17.txt.
                await ExecuteAsync(connection, transaction, employmentSql,
                    clientId, employment.EmployerName, employment.EmployerEin, employment.IncomeType,
                    employment.EmploymentType, employment.SeasonalEmploymentType,
                    ToYnChar(employment.IsPriorEmployment),
                    ToDbDate(employment.StartDate), ToDbDate(employment.EndDate),
                    employment.Source, employment.UpdateSw, employment.VaCmsSequenceNumber,
                    DataSource, DateTime.Today);

                var employmentId = NewId();

                foreach (var budget in employment.IncomeBudgetDetails ?? Enumerable.Empty<IncomeBudgetDetailsDTO>())
                {
                    await ExecuteAsync(connection, transaction, budgetSql,
                        clientId, employmentId, budget.MonthlyIncome, budget.MonthlyHours,
                        budget.IncomeMonth, budget.IncomeYear, budget.VerificationSource,
                        budget.Source, budget.UpdateSw, budget.VaCmsSequenceNumber, budget.ApplicationSource,
                        DataSource, DateTime.Today);
                }

                if (employment.ElectronicallyVerifiedData is { Count: > 0 } truvEmployers)
                    await InsertTruvEmployersAsync(connection, transaction, clientId, truvEmployers);
            }
        }

        // ── MWRP_CE_JOB_TRAINING / MWRP_CE_INKIND_UNPAID_WORK ─────────────────────
        // Identical shape apart from PROGRAM_TYPE, which only exists on JOB_TRAINING.

        private static async Task InsertJobTrainingLikeAsync(
            TdConnection connection, TdTransaction transaction, string table,
            string clientId, List<JobTrainingEntryDTO> entries, bool includeProgramType)
        {
            var sql =
                $"INSERT INTO {table} " +
                "(CLIENT_IDENTIFICATION_NBR, ORGANIZATION_ID, ORGANIZATION_NAME, MONTHLY_HOURS, " +
                "START_DATE, END_DATE, VERIFICATION_DATE, VERIFICATION_SOURCE, PROGRAM_NAME, " +
                (includeProgramType ? "PROGRAM_TYPE, " : "") +
                "SOURCE_SYSTEM, UPDATE_SW, VACMS_SEQUENCE_NUM, APPLICATION_SOURCE, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, " + (includeProgramType ? "?, " : "") + "?, ?, ?, ?, 'Y', ?, ?)";

            foreach (var entry in entries)
            {
                var values = new List<object?>
                {
                    clientId, entry.OrganizationId, entry.OrganizationName, entry.MonthlyHours,
                    ToDbDate(entry.StartDate), ToDbDate(entry.EndDate), ToDbDate(entry.VerificationDate),
                    entry.VerificationSource, entry.ProgramName,
                };

                if (includeProgramType)
                    values.Add(entry.ProgramType); // MWRP_CE_INKIND_UNPAID_WORK has no PROGRAM_TYPE column

                values.Add(entry.Source);
                values.Add(entry.UpdateSw);
                values.Add(entry.VaCmsSequenceNumber);
                values.Add(entry.ApplicationSource);
                values.Add(DataSource);
                values.Add(DateTime.Today);

                await ExecuteAsync(connection, transaction, sql, values.ToArray());
            }
        }

        // ── MWRP_CE_VOLUNTEERING_COMMUNITY_SERVICE ─────────────────────────────────

        private static async Task InsertVolunteeringAsync(
            TdConnection connection, TdTransaction transaction, string clientId, List<VolunteeringEntryDTO> volunteerings)
        {
            var sql =
                $"INSERT INTO {VolunteeringCommunityServiceTable} " +
                "(CLIENT_IDENTIFICATION_NBR, ORGANIZATION_ID, ORGANIZATION_NAME, MONTHLY_HOURS, " +
                "START_DATE, END_DATE, VERIFICATION_SOURCE, VERIFICATION_DATE, SOURCE_SYSTEM, UPDATE_SW, " +
                "VACMS_SEQUENCE_NUM, APPLICATION_SOURCE, EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            foreach (var volunteering in volunteerings)
            {
                await ExecuteAsync(connection, transaction, sql,
                    clientId, volunteering.OrganizationId, volunteering.OrganizationName,
                    volunteering.MonthlyHours, ToDbDate(volunteering.StartDate), ToDbDate(volunteering.EndDate),
                    volunteering.VerificationSource, ToDbDate(volunteering.VerificationDate),
                    volunteering.Source, volunteering.UpdateSw, volunteering.VaCmsSequenceNumber,
                    volunteering.ApplicationSource, DataSource, DateTime.Today);
            }
        }

        // ── MWRP_CE_EDUCATION_VERIFIED / MWRP_CE_NSC_ENROLLMENT / MWRP_CE_NSC_ENROLLMENT_DATA /
        //    MWRP_CE_COURSES_OF_STUDY ────────────────────────────────────────────────

        private static async Task InsertEducationAsync(
            TdConnection connection, TdTransaction transaction, string clientId, List<EducationEntryDTO> educationEntries)
        {
            var educationSql =
                $"INSERT INTO {EducationVerifiedTable} " +
                "(CLIENT_IDENTIFICATION_NBR, INSTITUTION_NAME, INSTITUTION_TYPE, VERIFICATION_DATE, " +
                "VERIFICATION_SOURCE, ENROLLMENT_STATUS, MONTHLY_HOURS, TERM_START_DATE, TERM_END_DATE, " +
                "SOURCE_SYSTEM, UPDATE_SW, VACMS_SEQUENCE_NUM, APPLICATION_SOURCE, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            var enrollmentSql =
                $"INSERT INTO {NscEnrollmentTable} " +
                "(CLIENT_IDENTIFICATION_NBR, OFFICIAL_SCHOOL_NAME, SCHOOL_CODE, BRANCH_CODE, " +
                "CURRENT_ENROLLMENT_STATUS, EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, 'Y', ?, ?)";

            var enrollmentDataSql =
                $"INSERT INTO {NscEnrollmentDataTable} " +
                "(CLIENT_IDENTIFICATION_NBR, ENROLLMENT_STATUS, TERM_BEGIN_DATE, TERM_END_DATE, " +
                "SCHOOL_CERTIFIED_ON_DATE, ANTICIPATED_GRADUATION_DATE, SOURCE_SYSTEM, UPDATE_SW, " +
                "VACMS_SEQUENCE_NUM, APPLICATION_SOURCE, EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            var courseOfStudySql =
                $"INSERT INTO {CoursesOfStudyTable} " +
                "(CLIENT_IDENTIFICATION_NBR, COURSE_NAME, NCES_CIP_CODE, SOURCE_SYSTEM, UPDATE_SW, " +
                "VACMS_SEQUENCE_NUM, EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            foreach (var education in educationEntries)
            {
                await ExecuteAsync(connection, transaction, educationSql,
                    clientId, education.InstitutionName, education.InstitutionType,
                    ToDbDate(education.VerificationDate), education.VerificationSource, education.EnrollmentStatus,
                    education.MonthlyHours, ToDbDate(education.TermStartDate), ToDbDate(education.TermEndDate),
                    education.Source, education.UpdateSw, education.VaCmsSequenceNumber, education.ApplicationSource,
                    DataSource, DateTime.Today);

                foreach (var nsc in education.ElectronicallyVerifiedData ?? Enumerable.Empty<NscEnrollmentDTO>())
                {
                    await ExecuteAsync(connection, transaction, enrollmentSql,
                        clientId, nsc.OfficialSchoolName, nsc.SchoolCode, nsc.BranchCode,
                        nsc.CurrentEnrollmentStatus, DataSource, DateTime.Today);

                    foreach (var period in nsc.EnrollmentData ?? Enumerable.Empty<NscEnrollmentDetailDTO>())
                    {
                        await ExecuteAsync(connection, transaction, enrollmentDataSql,
                            clientId, period.EnrollmentStatus, ToDbDate(period.TermBeginDate),
                            ToDbDate(period.TermEndDate), ToDbDate(period.SchoolCertifiedOnDate),
                            ToDbDate(period.AnticipatedGraduationDate),
                            period.Source, period.UpdateSw, period.VaCmsSequenceNumber, period.ApplicationSource,
                            DataSource, DateTime.Today);

                        foreach (var course in period.MajorCoursesOfStudy ?? Enumerable.Empty<CourseOfStudyDTO>())
                        {
                            await ExecuteAsync(connection, transaction, courseOfStudySql,
                                clientId, course.Course, course.NcesCipCode,
                                course.Source, course.UpdateSw, course.VaCmsSequenceNumber,
                                DataSource, DateTime.Today);
                        }
                    }
                }
            }
        }

        // ── MWRP_CE_ELECTRONICALLY_VERIFIED_EMPLOYMENT ─────────────────────────────

        private static async Task InsertElectronicallyVerifiedEmploymentAsync(
            TdConnection connection, TdTransaction transaction, string clientId,
            List<ElectronicallyVerifiedEmploymentDTO> summaries)
        {
            var sql =
                $"INSERT INTO {ElectronicallyVerifiedEmpTable} " +
                "(CLIENT_IDENTIFICATION_NBR, VERIFICATION_SOURCE, MONTHLY_INCOME, " +
                "MONTHLY_HOURS, INCOME_MONTH, INCOME_YEAR, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            foreach (var summary in summaries)
            {
                await ExecuteAsync(connection, transaction, sql,
                    clientId, summary.VerificationSource, summary.MonthlyIncome, summary.MonthlyHours,
                    summary.IncomeMonth, summary.IncomeYear, DataSource, DateTime.Today);
            }
        }

        // ── MWRP_CE_EV_INDV_INCOME_BUDGET ──────────────────────────────────────────
        // No CLIENT_ID column exists on this table in docs/Table_v17.txt; the client is
        // already identified via CLIENT_IDENTIFICATION_NBR, so IndvIncomeBudgetDTO.ClientId
        // has nowhere to go and is intentionally not persisted.

        private static async Task InsertIndvIncomeBudgetAsync(
            TdConnection connection, TdTransaction transaction, string clientId, List<IndvIncomeBudgetDTO> budgets)
        {
            var sql =
                $"INSERT INTO {EvIndvIncomeBudgetTable} " +
                "(CLIENT_IDENTIFICATION_NBR, EIN, BUDGET_BEGIN_DT, BUDGET_END_DT, " +
                "VERIFICATION_SOURCE, TOTAL_BUDGET_AMOUNT, EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            foreach (var budget in budgets)
            {
                await ExecuteAsync(connection, transaction, sql,
                    clientId, budget.Ein, ToDbDate(budget.BudgetBeginDate),
                    ToDbDate(budget.BudgetEndDate), budget.VerificationSource ?? budget.Source, budget.TotalBudgetAmount,
                    DataSource, DateTime.Today);
            }
        }

        // ── MWRP_CE_EV_EDG_INCOME_BUDGET / MWRP_CE_MAGI_INCOME_BUDGET (identical shape) ─

        private static async Task InsertEdgIncomeBudgetAsync(
            TdConnection connection, TdTransaction transaction, string table,
            string clientId, List<EdgIncomeBudgetDTO> budgets)
        {
            var sql =
                $"INSERT INTO {table} " +
                "(CLIENT_IDENTIFICATION_NBR, EDG_NUM, ELIG_BEGIN_DT, ELIG_END_DT, TOTAL_BUDGET_AMOUNT, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, 'Y', ?, ?)";

            foreach (var budget in budgets)
            {
                await ExecuteAsync(connection, transaction, sql,
                    clientId, budget.EdgNum, ToDbDate(budget.EligBeginDate),
                    ToDbDate(budget.EligEndDate), budget.TotalBudgetAmount, DataSource, DateTime.Today);
            }
        }

        // ── MWRP_CE_BUDGET_GROUP_MEMBER ────────────────────────────────────────────
        // No CLIENT_ID column here; ELIG_PART_STATUS_CD is VARCHAR(50) and stores the raw status string.

        private static async Task InsertBudgetGroupMembersAsync(
            TdConnection connection, TdTransaction transaction, string clientId, List<BudgetGroupMemberDTO> members)
        {
            var sql =
                $"INSERT INTO {BudgetGroupMemberTable} " +
                "(CLIENT_IDENTIFICATION_NBR, EDG_NUM, ELIG_PART_STATUS_CD, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, 'Y', ?, ?)";

            foreach (var member in members)
            {
                await ExecuteAsync(connection, transaction, sql,
                    clientId, member.EdgNum, member.EligPartStatusCd,
                    DataSource, DateTime.Today);
            }
        }

        // ── MWRP_CE_TRUV_EMPLOYER / MWRP_CE_PROVIDER / MWRP_CE_EMPLOYMENT_DETAIL /
        //    MWRP_CE_EMPLOYEE_PROFILE / MWRP_CE_PAY_STATEMENT / MWRP_CE_ANNUAL_INCOME ──
        // MWRP_CE_TRUV_EMPLOYER has no BIGINT id column of its own (only the varchar
        // TRUV_EMPLOYER_RECORD_ID); MWRP_CE_EMPLOYMENT_DETAIL likewise has no id column.
        // truvEmployerId/employmentDetailId below are generated purely to correlate the
        // child rows within this one save operation and are never written back onto
        // their own parent row.

        private static async Task InsertTruvEmployersAsync(
            TdConnection connection, TdTransaction transaction, string clientId, List<TruvEmployerDTO> truvEmployers)
        {
            var truvEmployerSql =
                $"INSERT INTO {TruvEmployerTable} " +
                "(CLIENT_IDENTIFICATION_NBR, TRUV_EMPLOYER_RECORD_ID, PRODUCT_TYPE, STATUS_CD, DATA_SOURCE, " +
                "COMPANY_NAME, IS_SUSPICIOUS_IND, START_DATE, END_DATE, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            var providerSql =
                $"INSERT INTO {ProviderTable} " +
                "(CLIENT_IDENTIFICATION_NBR, TRUV_EMPLOYER_ID, PAYROLL_PROVIDER_ID, PAYROLL_PROVIDER_NAME, PAYROLL_PROVIDER_LOGO_URL, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, 'Y', ?, ?)";

            var employmentDetailSql =
                $"INSERT INTO {EmploymentDetailTable} " +
                "(CLIENT_IDENTIFICATION_NBR, TRUV_EMPLOYER_ID, TRUV_EMPLOYMENT_ID, JOB_TITLE, JOB_TYPE, " +
                "START_DATE, END_DATE, IS_ACTIVE_IND, ANNUAL_INCOME, INCOME_UNIT, PAY_RATE, PAY_FREQUENCY, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            var employeeProfileSql =
                $"INSERT INTO {EmployeeProfileTable} " +
                "(CLIENT_IDENTIFICATION_NBR, EMPLOYMENT_DETAIL_ID, EMPLOYEE_ID, FULL_NAME, PAYROLL_PROVIDER_LOGO_URL, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, 'Y', ?, ?)";

            var payStatementSql =
                $"INSERT INTO {PayStatementTable} " +
                "(CLIENT_IDENTIFICATION_NBR, EMPLOYMENT_DETAIL_ID, TRUV_STATEMENT_ID, PAY_DATE, NET_PAY, " +
                "NET_PAY_YTD, GROSS_PAY, GROSS_PAY_YTD, BONUS, COMMISSION, HOURS_WORKED, " +
                "BASIS_OF_PAY, PERIOD_START, PERIOD_END, REGULAR_PAY, REGULAR_PAY_YTD, " +
                "OTHER_PAY, OTHER_PAY_YTD, BONUS_YTD, COMMISSION_YTD, OVERTIME_PAY, OVERTIME_YTD, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            var annualIncomeSql =
                $"INSERT INTO {AnnualIncomeTable} " +
                "(CLIENT_IDENTIFICATION_NBR, EMPLOYMENT_DETAIL_ID, TRUV_ANNUAL_INCOME_ID, INCOME_YEAR, REGULAR_PAY, " +
                "BONUS, COMMISSION, OVERTIME_PAY, OTHER_PAY, NET_PAY, GROSS_PAY, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            foreach (var truvEmployer in truvEmployers)
            {
                var truvEmployerId = NewId();
                await ExecuteAsync(connection, transaction, truvEmployerSql,
                    clientId, truvEmployer.Id, truvEmployer.ProductType,
                    truvEmployer.Status, truvEmployer.DataSource, truvEmployer.CompanyName,
                    ToYnChar(truvEmployer.IsSuspicious), ToDbDate(truvEmployer.StartDate),
                    ToDbDate(truvEmployer.EndDate), DataSource, DateTime.Today);

                if (truvEmployer.Provider is { } provider)
                {
                    await ExecuteAsync(connection, transaction, providerSql,
                        clientId, truvEmployerId, provider.Id, provider.Name, provider.LogoUrl,
                        DataSource, DateTime.Today);
                }

                foreach (var detail in truvEmployer.Employments ?? Enumerable.Empty<TruvEmploymentDetailDTO>())
                {
                    var employmentDetailId = NewId();

                    await ExecuteAsync(connection, transaction, employmentDetailSql,
                        clientId, truvEmployerId, detail.Id, detail.JobTitle,
                        detail.JobType, ToDbDate(detail.StartDate), ToDbDate(detail.EndDate),
                        ToYnChar(detail.IsActive), detail.Income, detail.IncomeUnit, detail.PayRate,
                        detail.PayFrequency, DataSource, DateTime.Today);

                    if (detail.Profile is { } profile)
                    {
                        await ExecuteAsync(connection, transaction, employeeProfileSql,
                            clientId, employmentDetailId, profile.EmployeeId, profile.FullName, profile.LogoUrl,
                            DataSource, DateTime.Today);
                    }

                    foreach (var statement in detail.Statement ?? Enumerable.Empty<TruvPayStatementDTO>())
                    {
                        await ExecuteAsync(connection, transaction, payStatementSql,
                            clientId, employmentDetailId, statement.Id, ToDbDate(statement.PayDate),
                            statement.NetPay, statement.NetPayYtd, statement.GrossPay, statement.GrossPayYtd,
                            statement.Bonus, statement.Commission, statement.Hours, statement.BasisOfPay,
                            ToDbDate(statement.PeriodStart), ToDbDate(statement.PeriodEnd), statement.Regular,
                            statement.RegularYtd, statement.OtherPay, statement.OtherPayYtd, statement.BonusYtd,
                            statement.CommissionYtd, statement.OverTime, statement.OverTimeYtd,
                            DataSource, DateTime.Today);
                    }

                    foreach (var annualIncome in detail.AnnualIncomeSummary ?? Enumerable.Empty<TruvAnnualIncomeDTO>())
                    {
                        await ExecuteAsync(connection, transaction, annualIncomeSql,
                            clientId, employmentDetailId, annualIncome.Id, annualIncome.Year,
                            annualIncome.Regular, annualIncome.Bonus, annualIncome.Commission, annualIncome.OverTime,
                            annualIncome.OtherPay, annualIncome.NetPay, annualIncome.GrossPay,
                            DataSource, DateTime.Today);
                    }
                }
            }
        }

        // ── MWRP_CE_RESPONSE ───────────────────────────────────────────────────────

        private static async Task InsertResponseAsync(
            TdConnection connection, TdTransaction transaction, long ackRowId, CepDWRequestDTO request,
            CepDWAckResponseDTO response)
        {
            // REQUEST_SEQUENCE_NBR is INTEGER here (unlike the VARCHAR column on MWRP_CE_REQUEST).
            object requestSequenceNumber = int.TryParse(request.RequestSequenceNumber, out var seqNum)
                ? seqNum
                : DBNull.Value;

            var sql =
                $"INSERT INTO {ResponseTable} " +
                "(ACK_ROW_ID, REQUEST_SEQUENCE_NBR, STATE_ID, REQUEST_SOURCE_CD, SOURCE_SYS_NM, UPDATE_SW, " +
                "ACK_CODE, ACK_STATUS, ACK_MESSAGE_TXT, MWRP_CE_REQUEST_ID, ACK_SOURCE_SYS_NM, ACK_UPDATE_SW, CREATED_TS, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            // Persist the exact acknowledgement fields that are returned to the caller, not a
            // separately-hardcoded copy, so the API response and MWRP_CE_RESPONSE never drift apart.
            await ExecuteAsync(connection, transaction, sql,
                ackRowId,
                requestSequenceNumber,
                request.State,
                request.RequestSource,
                DataSource,
                "YES",
                response.Acknowledgement.Code,
                response.Acknowledgement.Status,
                response.Acknowledgement.Message,
                response.Acknowledgement.NvhRequestId,
                DataSource,
                "YES",
                response.Acknowledgement.CreatedTimestamp,
                DataSource,
                DateTime.Today);
        }

        // ── Helpers ────────────────────────────────────────────────────────────────

        private static async Task ExecuteAsync(
            TdConnection connection, TdTransaction transaction, string sql, params object?[] values)
        {
            var placeholderCount = sql.Count(c => c == '?');
            if (placeholderCount != values.Length)
                throw new InvalidOperationException(
                    $"SQL placeholder/parameter count mismatch: {placeholderCount} '?' vs {values.Length} values. SQL: {sql}");

            await using var command = new TdCommand(sql, connection) { Transaction = transaction };

            foreach (var value in values)
                command.Parameters.Add(new TdParameter { Value = value ?? DBNull.Value });

            await command.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Monotonically increasing surrogate key generator (BIGINT-compatible). Seeded from
        /// UTC ticks so values keep increasing across app restarts. See the class-level note
        /// about why this is used instead of a Teradata IDENTITY column.
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

        /// <summary>Normalises a "YES"/"NO" text payload value to a single Y/N character.</summary>
        private static object ToYnCharFromText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return DBNull.Value;
            return value.Trim().Equals("YES", StringComparison.OrdinalIgnoreCase) ? "Y" : "N";
        }
    }
}
