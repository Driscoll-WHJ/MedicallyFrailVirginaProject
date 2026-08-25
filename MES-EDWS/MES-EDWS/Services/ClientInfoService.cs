using MES_EDWS.Models;
using Teradata.Client.Provider;

namespace MES_EDWS.Services
{
    /// <summary>
    /// Persists CEP-ICD-003 CE verification result payloads to the
    /// HR1_DMAS_POC.MWRP_CE_* Teradata tables (see docs/TableDefinition.txt).
    /// The whole payload is written inside one transaction so a partially-saved
    /// request is never left behind.
    ///
    /// NOTE: TableDefinition.txt does not declare GENERATED ALWAYS AS IDENTITY on any
    /// primary key, and the Teradata.Client.Provider ODBC driver has no reliable way to
    /// read back a server-generated identity value from a plain INSERT. Surrogate keys
    /// are therefore generated in application code (see <see cref="NewId"/>). If the
    /// deployed DDL turns out to use IDENTITY columns, these inserts will need to omit
    /// the *_ID columns and the ID retrieval strategy will need to be revisited with the DBA.
    /// </summary>
    public class ClientInfoService : IClientInfoService
    {
        private readonly string _connectionString;
        private readonly ILogger<ClientInfoService> _logger;

        private const string DataSource = "MES-EDWS";
        private const string Schema = "HR1_DMAS_POC";

        // ── Table names ────────────────────────────────────────────────────────────
        private const string RequestTable                     = Schema + ".MWRP_CE_REQUEST";
        private const string DocumentTable                    = Schema + ".MWRP_CE_DOCUMENT";
        private const string VerifiedTable                    = Schema + ".MWRP_CE_VERIFIED";
        private const string AidCategoryTable                 = Schema + ".MWRP_CE_AID_CATEGORY";
        private const string PhoneTable                       = Schema + ".MWRP_CE_PHONE";
        private const string AddressTable                     = Schema + ".MWRP_CE_ADDRESS";
        private const string CircumstanceTable                = Schema + ".MWRP_CE_CIRCUMSTANCE";
        private const string CircumstanceDetailTable           = Schema + ".MWRP_CE_CIRCUMSTANCE_DETAIL";
        private const string ExclusionPregnancyTable           = Schema + ".MWRP_CE_EXCLUSION_PREGNANCY";
        private const string ExclusionCaregiverTable           = Schema + ".MWRP_CE_EXCLUSION_CAREGIVER";
        private const string ExclusionFosterCareTable          = Schema + ".MWRP_CE_EXCLUSION_FOSTER_CARE";
        private const string ExclusionFormerFosterCareTable    = Schema + ".MWRP_CE_EXCLUSION_FORMER_FOSTER_CARE";
        private const string IncarcerationTable                = Schema + ".MWRP_CE_INCARCERATION";
        private const string MedicarePartAbTable                = Schema + ".MWRP_CE_MEDICARE_PART_AB";
        private const string FrailtyTable                      = Schema + ".MWRP_CE_FRAILTY";
        private const string EmploymentTable                   = Schema + ".MWRP_CE_EMPLOYMENT";
        private const string IncomeBudgetTable                 = Schema + ".MWRP_CE_INCOME_BUDGET";
        private const string EducationTable                    = Schema + ".MWRP_CE_EDUCATION";
        private const string NscEnrollmentTable                = Schema + ".MWRP_CE_NSC_ENROLLMENT";
        private const string NscEnrollmentDetailTable           = Schema + ".MWRP_CE_NSC_ENROLLMENT_DETAIL";
        private const string CourseOfStudyTable                = Schema + ".MWRP_CE_COURSE_OF_STUDY";
        private const string JobTrainingTable                  = Schema + ".MWRP_CE_JOB_TRAINING";
        private const string VolunteerServiceTable              = Schema + ".MWRP_CE_VOLUNTEER_SERVICE";
        private const string ElectronicallyVerifiedEmpTable    = Schema + ".MWRP_CE_ELECTRONICALLY_VERIFIED_EMPLOYMENT";
        private const string TruvEmployerTable                  = Schema + ".MWRP_CE_TRUV_EMPLOYER";
        private const string TruvProviderTable                  = Schema + ".MWRP_CE_TRUV_PROVIDER";
        private const string EmploymentDetailTable              = Schema + ".MWRP_CE_EMPLOYMENT_DETAIL";
        private const string EmployeeProfileTable               = Schema + ".MWRP_CE_EMPLOYEE_PROFILE";
        private const string PayStatementTable                  = Schema + ".MWRP_CE_PAY_STATEMENT";
        private const string AnnualIncomeTable                  = Schema + ".MWRP_CE_ANNUAL_INCOME";
        private const string RequestAckTable                    = Schema + ".MWRP_CE_REQUEST_ACK";

        public ClientInfoService(IConfiguration configuration, ILogger<ClientInfoService> logger)
        {
            _connectionString = configuration.GetConnectionString("TeradataConnection")
                ?? throw new InvalidOperationException("TeradataConnection connection string is not configured.");
            _logger = logger;
        }

        // ── Entry point ────────────────────────────────────────────────────────────

        public async Task<long> SaveCeVerificationResultsAsync(CepDWRequestDTO request)
        {
            var requestRowId = NewId();

            await using var connection = new TdConnection(_connectionString);
            await connection.OpenAsync();

            await using var transaction = connection.BeginTransaction();

            try
            {
                await InsertRequestAsync(connection, transaction, requestRowId, request);

                if (request.DocumentsUploadedInMwrp is { Count: > 0 } documents)
                    await InsertDocumentsAsync(connection, transaction, requestRowId, documents);

                await InsertVerifiedAsync(connection, transaction, requestRowId, request.CeVerified);

                await InsertRequestAckAsync(connection, transaction, requestRowId, request);

                transaction.Commit();

                _logger.LogInformation(
                    "CE verification results saved to Teradata. RequestRowId: {RequestRowId}, " +
                    "CaseNumber: {CaseNumber}", requestRowId, request.CaseNumber);

                return requestRowId;
            }
            catch (Exception ex)
            {
                try { transaction.Rollback(); }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx,
                        "Rollback failed after save error. RequestRowId: {RequestRowId}", requestRowId);
                }

                _logger.LogError(ex,
                    "Failed to save CE verification results. RequestRowId: {RequestRowId}, " +
                    "CaseNumber: {CaseNumber}", requestRowId, request.CaseNumber);
                throw;
            }
        }

        // ── MWRP_CE_REQUEST ────────────────────────────────────────────────────────

        private static async Task InsertRequestAsync(
            TdConnection connection, TdTransaction transaction, long requestRowId, CepDWRequestDTO request)
        {
            var sql =
                $"INSERT INTO {RequestTable} " +
                "(REQUEST_ROW_ID, REQUEST_TIMESTAMP, REQUEST_SEQUENCE_NUMBER, STATE_ID, REQUEST_SOURCE_CD, " +
                "CASE_NUMBER, SEND_EMAIL_TO_CUSTOMER_FLG, PREFERRED_COMM_LANGUAGE_CD, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            await ExecuteAsync(connection, transaction, sql,
                requestRowId,
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
            TdConnection connection, TdTransaction transaction, long requestRowId, List<DocumentUploadDTO> documents)
        {
            var sql =
                $"INSERT INTO {DocumentTable} " +
                "(DOCUMENT_ROW_ID, REQUEST_ROW_ID, DOCUMENT_TYPE_CD, DOCUMENT_SUBTYPE_CD, CLIENT_ID, DOCUMENT_ID, " +
                "DOCUMENT_UPLOAD_TS, DOCUMENT_DELETED_TS, SOURCE_SYSTEM_CD, DOCUMENT_STATUS_CD, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            foreach (var document in documents)
            {
                await ExecuteAsync(connection, transaction, sql,
                    NewId(),
                    requestRowId,
                    document.DocumentType,
                    document.DocumentSubtype,
                    document.ClientId,
                    document.DocumentId,
                    document.DocumentUploadTimestamp,
                    document.DocumentDeletedTimestamp,
                    document.SourceSystem,
                    document.DocumentStatus,
                    DataSource,
                    DateTime.Today);
            }
        }

        // ── MWRP_CE_VERIFIED and everything hanging off it ─────────────────────────

        private async Task InsertVerifiedAsync(
            TdConnection connection, TdTransaction transaction, long requestRowId, CeVerifiedDTO verified)
        {
            var verifiedId = NewId();

            var sql =
                $"INSERT INTO {VerifiedTable} " +
                "(CE_VERIFIED_ID, REQUEST_ROW_ID, CLIENT_IDENTIFICATION_NBR, MMIS_ENROLLEE_ID, SSN, MWR_STATUS_CD, " +
                "MWR_START_DT, MWR_END_DT, PREV_ELIG_AUTH_DT, CURRENT_ELIGIBILITY_BEGIN_DT, APP_CHANGE_SUBMISSION_DT, " +
                "RENEWAL_INITIATED_DT, CASE_ACTION_CD, DUE_DT, EMAIL_VERIFIED_FLG, EMAIL_ADDR, ALT_EMAIL_ADDR, " +
                "PRIMARY_CONTACT_METHOD_CD, HEAD_OF_HOUSEHOLD_FLG, REQUIRES_CE_EVALUATION_FLG, RELATIONSHIP_TO_HOH_CD, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            await ExecuteAsync(connection, transaction, sql,
                verifiedId,
                requestRowId,
                verified.ClientIdentificationNumber,
                verified.MmisEnrolleeId,
                verified.SocialSecurityNumber,
                verified.MwrStatus,
                ToDbDate(verified.MwrStartDate),
                ToDbDate(verified.MwrEndDate),
                ToDbDate(verified.PreviousEligibilityAuthorizationDate),
                ToDbDate(verified.CurrentEligibilityBeginDate),
                ToDbDate(verified.ApplicationChangeSubmissionDate),
                ToDbDate(verified.RenewalInitiatedDate),
                verified.CaseAction,
                ToDbDate(verified.DueDate),
                ToYnChar(verified.EmailVerifiedFlag),
                verified.Email,
                verified.AlternateEmail,
                verified.PrimaryModeOfCommunication,
                ToYnChar(verified.HeadOfHousehold),
                ToYnChar(verified.RequiresCeEvaluation),
                verified.RelationshipWithHoh,
                DataSource,
                DateTime.Today);

            if (verified.AidCategory is { Count: > 0 } aidCategories)
                await InsertAidCategoriesAsync(connection, transaction, verifiedId, aidCategories);

            if (verified.PhoneData is { Count: > 0 } phones)
                await InsertPhonesAsync(connection, transaction, verifiedId, phones);

            if (verified.Addresses is { Count: > 0 } addresses)
                await InsertAddressesAsync(connection, transaction, verifiedId, addresses);

            if (verified.Exclusions != null)
                await InsertExclusionsAsync(connection, transaction, verifiedId, verified.Exclusions);

            if (verified.Exceptions?.CircumstanceForException != null)
                await InsertCircumstanceAsync(
                    connection, transaction, verifiedId, verified.Exceptions.CircumstanceForException);

            if (verified.Engagements != null)
                await InsertEngagementsAsync(connection, transaction, verifiedId, verified.Engagements);
        }

        // ── MWRP_CE_AID_CATEGORY ───────────────────────────────────────────────────

        private static async Task InsertAidCategoriesAsync(
            TdConnection connection, TdTransaction transaction, long verifiedId, List<AidCategoryDTO> aidCategories)
        {
            var sql =
                $"INSERT INTO {AidCategoryTable} " +
                "(AID_CATEGORY_ID, CE_VERIFIED_ID, AID_CATEGORY_CD, START_DT, END_DT, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, 'Y', ?, ?)";

            foreach (var aidCategory in aidCategories)
            {
                await ExecuteAsync(connection, transaction, sql,
                    NewId(),
                    verifiedId,
                    aidCategory.Code,
                    ToDbDate(aidCategory.StartDate),
                    ToDbDate(aidCategory.EndDate),
                    DataSource,
                    DateTime.Today);
            }
        }

        // ── MWRP_CE_PHONE ──────────────────────────────────────────────────────────

        private static async Task InsertPhonesAsync(
            TdConnection connection, TdTransaction transaction, long verifiedId, List<PhoneDataDTO> phones)
        {
            var sql =
                $"INSERT INTO {PhoneTable} " +
                "(PHONE_ID, CE_VERIFIED_ID, PHONE_TYPE_CD, PHONE_NBR, EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, 'Y', ?, ?)";

            foreach (var phone in phones)
            {
                await ExecuteAsync(connection, transaction, sql,
                    NewId(),
                    verifiedId,
                    phone.PhoneType,
                    phone.PhoneNumber,
                    DataSource,
                    DateTime.Today);
            }
        }

        // ── MWRP_CE_ADDRESS ────────────────────────────────────────────────────────

        private static async Task InsertAddressesAsync(
            TdConnection connection, TdTransaction transaction, long verifiedId, List<AddressDTO> addresses)
        {
            var sql =
                $"INSERT INTO {AddressTable} " +
                "(ADDRESS_ID, CE_VERIFIED_ID, ADDRESS_TYPE_CD, ADDRESS_LINE1, ADDRESS_LINE2, COUNTY_NM, CITY_NM, " +
                "ZIP_CD5, ZIP_CD4, STATE_ID, FIPS_CD, EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            foreach (var address in addresses)
            {
                await ExecuteAsync(connection, transaction, sql,
                    NewId(),
                    verifiedId,
                    address.AddressType,
                    address.AddressLine1,
                    address.AddressLine2,
                    address.County,
                    address.City,
                    address.ZipCode5,
                    address.ZipCode4,
                    address.State,
                    address.FipsCode,
                    DataSource,
                    DateTime.Today);
            }
        }

        // ── MWRP_CE_EXCLUSION_* ────────────────────────────────────────────────────

        private async Task InsertExclusionsAsync(
            TdConnection connection, TdTransaction transaction, long verifiedId, ExclusionsDTO exclusions)
        {
            if (exclusions.Pregnancy is { } pregnancy)
            {
                var sql =
                    $"INSERT INTO {ExclusionPregnancyTable} " +
                    "(PREGNANCY_ID, CE_VERIFIED_ID, CIRCUMSTANCE_CD, EXPECTED_DUE_DT, ACTUAL_PREGNANCY_END_DT, " +
                    "EFFECTIVE_BEGIN_DT, EFFECTIVE_END_DT, EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                    "VALUES (?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

                await ExecuteAsync(connection, transaction, sql,
                    NewId(), verifiedId, pregnancy.CircumstanceCode,
                    ToDbDate(pregnancy.ExpectedDueDate), ToDbDate(pregnancy.ActualPregnancyEndDate),
                    ToDbDate(pregnancy.EffectiveBeginDate), ToDbDate(pregnancy.EffectiveEndDate),
                    DataSource, DateTime.Today);
            }

            if (exclusions.CareGiverCircumstance is { } caregiver)
            {
                var sql =
                    $"INSERT INTO {ExclusionCaregiverTable} " +
                    "(CAREGIVER_ID, CE_VERIFIED_ID, CIRCUMSTANCE_CD, RELATIONSHIP_WITH_DEPENDENT_CD, DEPENDENT_DOB, " +
                    "DEPENDENT_LIVES_SAME_HOME_FLG, DEPENDENT_DISABLED_FLG, CAREGIVING_HOURS_PER_WEEK, " +
                    "EFFECTIVE_BEGIN_DT, EFFECTIVE_END_DT, EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                    "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

                await ExecuteAsync(connection, transaction, sql,
                    NewId(), verifiedId, caregiver.CircumstanceCode, caregiver.RelationshipWithDependent,
                    ToDbDate(caregiver.DependentDob), ToYnChar(caregiver.DependentLivesSameHome),
                    ToYnChar(caregiver.DependentDisabled), caregiver.CaregivingHoursPerWeek,
                    ToDbDate(caregiver.EffectiveBeginDate), ToDbDate(caregiver.EffectiveEndDate),
                    DataSource, DateTime.Today);
            }

            if (exclusions.FosterCare is { } fosterCare)
            {
                var sql =
                    $"INSERT INTO {ExclusionFosterCareTable} " +
                    "(FOSTER_CARE_ID, CE_VERIFIED_ID, CIRCUMSTANCE_CD, RECEIVING_IVE_FOSTER_CARE_FLG, " +
                    "IN_STATE_CUSTODY_FLG, EFFECTIVE_BEGIN_DT, EFFECTIVE_END_DT, " +
                    "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                    "VALUES (?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

                await ExecuteAsync(connection, transaction, sql,
                    NewId(), verifiedId, fosterCare.CircumstanceCode, ToYnChar(fosterCare.ReceivingIveFosterCare),
                    ToYnChar(fosterCare.InStateCustody), ToDbDate(fosterCare.EffectiveBeginDate),
                    ToDbDate(fosterCare.EffectiveEndDate), DataSource, DateTime.Today);
            }

            if (exclusions.FormerFosterCare is { } formerFosterCare)
            {
                var sql =
                    $"INSERT INTO {ExclusionFormerFosterCareTable} " +
                    "(FORMER_FOSTER_CARE_ID, CE_VERIFIED_ID, CIRCUMSTANCE_CD, " +
                    "ENROLLED_MEDICAID_FOSTERCARE_AGE18_FLG, EFFECTIVE_BEGIN_DT, EFFECTIVE_END_DT, " +
                    "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                    "VALUES (?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

                await ExecuteAsync(connection, transaction, sql,
                    NewId(), verifiedId, formerFosterCare.CircumstanceCode,
                    ToYnChar(formerFosterCare.EnrolledMedicaidFosterCareAge18),
                    ToDbDate(formerFosterCare.EffectiveBeginDate), ToDbDate(formerFosterCare.EffectiveEndDate),
                    DataSource, DateTime.Today);
            }

            if (exclusions.IncarceratedCircumstance is { } incarceration)
            {
                var sql =
                    $"INSERT INTO {IncarcerationTable} " +
                    "(INCARCERATION_ID, CE_VERIFIED_ID, CIRCUMSTANCE_CD, TOA_CD, LIVING_ARRANGEMENT_TYPE_CD, " +
                    "INCARCERATED_LAST_3_MONTHS_FLG, FACILITY_TYPE_CD, EFFECTIVE_BEGIN_DT, EFFECTIVE_END_DT, " +
                    "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                    "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

                await ExecuteAsync(connection, transaction, sql,
                    NewId(), verifiedId, incarceration.CircumstanceCode, incarceration.Toa,
                    incarceration.LivingArrangementType, ToYnChar(incarceration.IncarceratedLast3Months),
                    incarceration.FacilityType, ToDbDate(incarceration.EffectiveBeginDate),
                    ToDbDate(incarceration.EffectiveEndDate), DataSource, DateTime.Today);
            }

            if (exclusions.MedicarePartAB is { } medicare)
            {
                var sql =
                    $"INSERT INTO {MedicarePartAbTable} " +
                    "(MEDICARE_PART_AB_ID, CE_VERIFIED_ID, COVERAGE_TYPE_CD, MEDICARE_EXPENSE_TYPE_CD, SOLQ_FLG, " +
                    "START_DT, END_DT, EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                    "VALUES (?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

                await ExecuteAsync(connection, transaction, sql,
                    NewId(), verifiedId, medicare.CoverageType, medicare.MedicareExpenseType,
                    ToYnChar(medicare.Solq), ToDbDate(medicare.StartDate), ToDbDate(medicare.EndDate),
                    DataSource, DateTime.Today);
            }

            if (exclusions.Frailty is { } frailty)
            {
                var sql =
                    $"INSERT INTO {FrailtyTable} " +
                    "(FRAILTY_ID, CE_VERIFIED_ID, SERIOUS_MEDICAL_CONDITION_TXT, " +
                    "MENTAL_HEALTH_PHYSICAL_LIMITATION_TXT, SERIOUS_HEALTH_IMPACT_FLG, MENTAL_HEALTH_FLG, " +
                    "PHYSICAL_DISABILITY_FLG, VERIFICATION_CD, EFFECTIVE_START_DT, EFFECTIVE_END_DT, " +
                    "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                    "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

                await ExecuteAsync(connection, transaction, sql,
                    NewId(), verifiedId, frailty.SeriousMedicalConditionText,
                    frailty.MentalHealthPhysicalLimitationText, ToYnChar(frailty.SeriousHealthImpact),
                    ToYnChar(frailty.MentalHealth), ToYnChar(frailty.PhysicalDisability), frailty.VerificationCode,
                    ToDbDate(frailty.EffectiveStartDate), ToDbDate(frailty.EffectiveEndDate),
                    DataSource, DateTime.Today);
            }

            if (exclusions.CircumstanceForExclusion != null)
                await InsertCircumstanceAsync(connection, transaction, verifiedId, exclusions.CircumstanceForExclusion);
        }

        // ── MWRP_CE_CIRCUMSTANCE / MWRP_CE_CIRCUMSTANCE_DETAIL ────────────────────

        private static async Task InsertCircumstanceAsync(
            TdConnection connection, TdTransaction transaction, long verifiedId, CircumstanceDTO circumstance)
        {
            var circumstanceId = NewId();

            var circumstanceSql =
                $"INSERT INTO {CircumstanceTable} " +
                "(CIRCUMSTANCE_ID, CE_VERIFIED_ID, CIRCUMSTANCE_CD, CIRCUMSTANCE_DESC, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, 'Y', ?, ?)";

            await ExecuteAsync(connection, transaction, circumstanceSql,
                circumstanceId, verifiedId, circumstance.CircumstanceCode, circumstance.CircumstanceDescription,
                DataSource, DateTime.Today);

            var detailSql =
                $"INSERT INTO {CircumstanceDetailTable} " +
                "(DETAIL_ID, CIRCUMSTANCE_ID, START_DT, END_DT, ONGOING_PERMANENT_FLG, VERIFICATION_SOURCE_CD, " +
                "VERIFICATION_DT, EFFECTIVE_BEGIN_DT, EFFECTIVE_END_DT, AMERICAN_INDIAN_ALASKAN_NATIVE_FLG, " +
                "HOSPITAL_CARE_FLG, EXTENDED_MEDICAL_TRAVEL_FLG, SUBSTANCE_USE_DISORDER_FLG, " +
                "VETERAN_100_PERCENT_DISABLED_FLG, BLIND_OR_DISABLED_FLG, TANF_ENROLLED_FLG, SNAP_APPROVED_FLG, " +
                "SNAP_WORK_REQ_NOT_EXEMPT_FLG, EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            await ExecuteAsync(connection, transaction, detailSql,
                NewId(), circumstanceId,
                ToDbDate(circumstance.StartDate), ToDbDate(circumstance.EndDate),
                ToYnChar(circumstance.OnGoingPermanent), circumstance.VerificationSource,
                ToDbDate(circumstance.VerificationDate),
                ToDbDate(circumstance.EffectiveBeginDate), ToDbDate(circumstance.EffectiveEndDate),
                ToYnChar(circumstance.AmericanIndianAlaskanNative), ToYnChar(circumstance.HospitalCare),
                ToYnChar(circumstance.ExtendedMedicalTravel), ToYnChar(circumstance.SubstanceUseDisorder),
                ToYnChar(circumstance.Veteran100PercentDisabled), ToYnChar(circumstance.BlindOrDisabled),
                ToYnChar(circumstance.TanfEnrolled), ToYnChar(circumstance.SnapApproved),
                ToYnChar(circumstance.SnapWorkReqNotExempt),
                DataSource, DateTime.Today);
        }

        // ── Engagements: employment, job training, education, volunteering ────────

        private async Task InsertEngagementsAsync(
            TdConnection connection, TdTransaction transaction, long verifiedId, EngagementsDTO engagements)
        {
            if (engagements.Employment is { Count: > 0 } employments)
                await InsertEmploymentAsync(connection, transaction, verifiedId, employments);

            if (engagements.JobTraining is { Count: > 0 } jobTraining)
                await InsertJobTrainingAsync(connection, transaction, verifiedId, jobTraining);

            if (engagements.Education is { Count: > 0 } education)
                await InsertEducationAsync(connection, transaction, verifiedId, education);

            if (engagements.VolunteeringCommunityService is { Count: > 0 } volunteering)
                await InsertVolunteeringAsync(connection, transaction, verifiedId, volunteering);

            if (engagements.EvIndvIncomeBudget is { Count: > 0 } evIncomeBudgets)
                await InsertElectronicallyVerifiedEmploymentAsync(connection, transaction, verifiedId, evIncomeBudgets);

            // Truv/payroll-provider verified employers reference CE_EMPLOYMENT via EMPLOYMENT_ID.
            // The payload carries this list as a sibling of the self-reported employment[] array
            // rather than nested inside it, so a minimal CE_EMPLOYMENT row is created per verified
            // employer purely to satisfy that foreign key.
            if (engagements.ElectronicallyVerifiedEmployment is { Count: > 0 } truvEmployers)
                await InsertTruvEmployersAsync(connection, transaction, verifiedId, truvEmployers);
        }

        // ── MWRP_CE_EMPLOYMENT / MWRP_CE_INCOME_BUDGET ─────────────────────────────

        private static async Task InsertEmploymentAsync(
            TdConnection connection, TdTransaction transaction, long verifiedId, List<EmploymentEntryDTO> employments)
        {
            var employmentSql =
                $"INSERT INTO {EmploymentTable} " +
                "(EMPLOYMENT_ID, CE_VERIFIED_ID, EMPLOYER_NM, EMPLOYER_EIN, EMPLOYMENT_TYPE_CD, " +
                "SEASONAL_EMPLOYMENT_FLG, SEASONAL_EMPLOYMENT_TYPE_CD, PRIOR_EMPLOYMENT_FLG, START_DT, END_DT, " +
                "EMPLOYMENT_ACTIVE_FLG, IN_KIND_UNPAID_FLG, EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            var budgetSql =
                $"INSERT INTO {IncomeBudgetTable} " +
                "(BUDGET_ID, EMPLOYMENT_ID, MONTHLY_INCOME_AMT, MONTHLY_HOURS_QTY, INCOME_MONTH_NBR, " +
                "INCOME_YEAR_NBR, VERIFICATION_SOURCE_CD, EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            foreach (var employment in employments)
            {
                var employmentId = NewId();

                await ExecuteAsync(connection, transaction, employmentSql,
                    employmentId, verifiedId, employment.EmployerName, employment.EmployerEin,
                    employment.EmploymentType, ToYnChar(employment.IsSeasonalEmployment),
                    employment.SeasonalEmploymentType, ToYnChar(employment.IsPriorEmployment),
                    ToDbDate(employment.StartDate), ToDbDate(employment.EndDate),
                    ToYnChar(employment.IsEmploymentActive), ToYnChar(employment.IsInkindOrUnPaid),
                    DataSource, DateTime.Today);

                if (employment.IncomeBudgetDetails is { } budget)
                {
                    await ExecuteAsync(connection, transaction, budgetSql,
                        NewId(), employmentId, budget.MonthlyIncome, budget.MonthlyHours,
                        budget.IncomeMonth, budget.IncomeYear, budget.VerificationSource,
                        DataSource, DateTime.Today);
                }
            }
        }

        // ── MWRP_CE_JOB_TRAINING ───────────────────────────────────────────────────
        // NOTE: the payload's "programName" has no corresponding column and is not persisted.

        private static async Task InsertJobTrainingAsync(
            TdConnection connection, TdTransaction transaction, long verifiedId, List<JobTrainingEntryDTO> jobTrainings)
        {
            var sql =
                $"INSERT INTO {JobTrainingTable} " +
                "(JOB_TRAINING_ID, CE_VERIFIED_ID, ORGANIZATION_ID, ORGANIZATION_NM, MONTHLY_HOURS_QTY, " +
                "START_DT, END_DT, VERIFICATION_DT, VERIFICATION_SOURCE_CD, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            foreach (var training in jobTrainings)
            {
                await ExecuteAsync(connection, transaction, sql,
                    NewId(), verifiedId, training.OrganizationId, training.OrganizationName, training.MonthlyHours,
                    ToDbDate(training.StartDate), ToDbDate(training.EndDate), ToDbDate(training.VerificationDate),
                    training.VerificationSource, DataSource, DateTime.Today);
            }
        }

        // ── MWRP_CE_VOLUNTEER_SERVICE ──────────────────────────────────────────────

        private static async Task InsertVolunteeringAsync(
            TdConnection connection, TdTransaction transaction, long verifiedId, List<VolunteeringEntryDTO> volunteerings)
        {
            var sql =
                $"INSERT INTO {VolunteerServiceTable} " +
                "(VOLUNTEER_SERVICE_ID, CE_VERIFIED_ID, ORGANIZATION_ID, ORGANIZATION_NM, MONTHLY_HOURS_QTY, " +
                "START_DT, END_DT, VERIFICATION_SOURCE_CD, VERIFICATION_DT, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            foreach (var volunteering in volunteerings)
            {
                await ExecuteAsync(connection, transaction, sql,
                    NewId(), verifiedId, volunteering.OrganizationId, volunteering.OrganizationName,
                    volunteering.MonthlyHours, ToDbDate(volunteering.StartDate), ToDbDate(volunteering.EndDate),
                    volunteering.VerificationSource, ToDbDate(volunteering.VerificationDate),
                    DataSource, DateTime.Today);
            }
        }

        // ── MWRP_CE_EDUCATION / MWRP_CE_NSC_ENROLLMENT / MWRP_CE_NSC_ENROLLMENT_DETAIL /
        //    MWRP_CE_COURSE_OF_STUDY ─────────────────────────────────────────────────

        private static async Task InsertEducationAsync(
            TdConnection connection, TdTransaction transaction, long verifiedId, List<EducationEntryDTO> educationEntries)
        {
            var educationSql =
                $"INSERT INTO {EducationTable} " +
                "(EDUCATION_ID, CE_VERIFIED_ID, INSTITUTION_NM, INSTITUTION_TYPE_CD, VERIFICATION_DT, " +
                "VERIFICATION_SOURCE_CD, ENROLLMENT_STATUS_CD, MONTHLY_HOURS_QTY, TERM_START_DT, TERM_END_DT, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            var enrollmentSql =
                $"INSERT INTO {NscEnrollmentTable} " +
                "(NSC_ENROLLMENT_ID, EDUCATION_ID, OFFICIAL_SCHOOL_NM, SCHOOL_CD, BRANCH_CD, " +
                "CURRENT_ENROLLMENT_STATUS_CD, EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            var enrollmentDetailSql =
                $"INSERT INTO {NscEnrollmentDetailTable} " +
                "(ENROLLMENT_DETAIL_ID, NSC_ENROLLMENT_ID, ENROLLMENT_STATUS_CD, TERM_BEGIN_DT, TERM_END_DT, " +
                "SCHOOL_CERTIFIED_DT, ANTICIPATED_GRADUATION_DT, EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            var courseOfStudySql =
                $"INSERT INTO {CourseOfStudyTable} " +
                "(COURSE_OF_STUDY_ID, ENROLLMENT_DETAIL_ID, COURSE_NM, NCES_CIP_CD, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, 'Y', ?, ?)";

            foreach (var education in educationEntries)
            {
                var educationId = NewId();

                await ExecuteAsync(connection, transaction, educationSql,
                    educationId, verifiedId, education.InstitutionName, education.InstitutionType,
                    ToDbDate(education.VerificationDate), education.VerificationSource, education.EnrollmentStatus,
                    education.MonthlyHours, ToDbDate(education.TermStartDate), ToDbDate(education.TermEndDate),
                    DataSource, DateTime.Today);

                foreach (var nsc in education.ElectronicallyVerifiedData ?? Enumerable.Empty<NscEnrollmentDTO>())
                {
                    var nscId = NewId();

                    await ExecuteAsync(connection, transaction, enrollmentSql,
                        nscId, educationId, nsc.OfficialSchoolName, nsc.SchoolCode, nsc.BranchCode,
                        nsc.CurrentEnrollmentStatus, DataSource, DateTime.Today);

                    foreach (var period in nsc.EnrollmentData ?? Enumerable.Empty<NscEnrollmentDetailDTO>())
                    {
                        var periodId = NewId();

                        await ExecuteAsync(connection, transaction, enrollmentDetailSql,
                            periodId, nscId, period.EnrollmentStatus, ToDbDate(period.TermBeginDate),
                            ToDbDate(period.TermEndDate), ToDbDate(period.SchoolCertifiedOnDate),
                            ToDbDate(period.AnticipatedGraduationDate), DataSource, DateTime.Today);

                        foreach (var course in period.MajorCoursesOfStudy ?? Enumerable.Empty<CourseOfStudyDTO>())
                        {
                            await ExecuteAsync(connection, transaction, courseOfStudySql,
                                NewId(), periodId, course.Course, course.NcesCipCode, DataSource, DateTime.Today);
                        }
                    }
                }
            }
        }

        // ── MWRP_CE_ELECTRONICALLY_VERIFIED_EMPLOYMENT ─────────────────────────────

        private static async Task InsertElectronicallyVerifiedEmploymentAsync(
            TdConnection connection, TdTransaction transaction, long verifiedId,
            List<ElectronicallyVerifiedEmploymentDTO> summaries)
        {
            var sql =
                $"INSERT INTO {ElectronicallyVerifiedEmpTable} " +
                "(VERIFIED_EMPLOYMENT_ID, CE_VERIFIED_ID, VERIFICATION_SOURCE_CD, MONTHLY_INCOME_AMT, " +
                "MONTHLY_HOURS_QTY, INCOME_MONTH_NBR, INCOME_YEAR_NBR, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            foreach (var summary in summaries)
            {
                await ExecuteAsync(connection, transaction, sql,
                    NewId(), verifiedId, summary.VerificationSource, summary.MonthlyIncome, summary.MonthlyHours,
                    summary.IncomeMonth, summary.IncomeYear, DataSource, DateTime.Today);
            }
        }

        // ── MWRP_CE_TRUV_EMPLOYER / MWRP_CE_TRUV_PROVIDER / MWRP_CE_EMPLOYMENT_DETAIL /
        //    MWRP_CE_EMPLOYEE_PROFILE / MWRP_CE_PAY_STATEMENT / MWRP_CE_ANNUAL_INCOME ──

        private static async Task InsertTruvEmployersAsync(
            TdConnection connection, TdTransaction transaction, long verifiedId, List<TruvEmployerDTO> truvEmployers)
        {
            var placeholderEmploymentSql =
                $"INSERT INTO {EmploymentTable} " +
                "(EMPLOYMENT_ID, CE_VERIFIED_ID, EMPLOYER_NM, EMPLOYMENT_TYPE_CD, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, 'Y', ?, ?)";

            var truvEmployerSql =
                $"INSERT INTO {TruvEmployerTable} " +
                "(TRUV_EMPLOYER_ID, EMPLOYMENT_ID, TRUV_EXTERNAL_ID, PRODUCT_TYPE_CD, STATUS_CD, DATA_SOURCE_CD, " +
                "COMPANY_NM, SUSPICIOUS_FLG, EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            var truvProviderSql =
                $"INSERT INTO {TruvProviderTable} " +
                "(PROVIDER_ROW_ID, TRUV_EMPLOYER_ID, PROVIDER_ID, PROVIDER_NM, PROVIDER_LOGO_URL, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, 'Y', ?, ?)";

            var employmentDetailSql =
                $"INSERT INTO {EmploymentDetailTable} " +
                "(EMPLOYMENT_DETAIL_ID, TRUV_EMPLOYER_ID, EMPLOYMENT_EXTERNAL_ID, JOB_TITLE, JOB_TYPE_CD, " +
                "START_DT, END_DT, ACTIVE_FLG, ANNUAL_INCOME_AMT, INCOME_UNIT_CD, PAY_RATE_AMT, PAY_FREQUENCY_CD, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            var employeeProfileSql =
                $"INSERT INTO {EmployeeProfileTable} " +
                "(EMPLOYEE_PROFILE_ID, EMPLOYMENT_DETAIL_ID, EMPLOYEE_ID, EMPLOYEE_FULL_NM, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, 'Y', ?, ?)";

            var payStatementSql =
                $"INSERT INTO {PayStatementTable} " +
                "(PAY_STATEMENT_ID, EMPLOYMENT_DETAIL_ID, PAY_STATEMENT_EXT_ID, PAY_DT, NET_PAY_AMT, " +
                "NET_PAY_YTD_AMT, GROSS_PAY_AMT, GROSS_PAY_YTD_AMT, BONUS_AMT, COMMISSION_AMT, HOURS_QTY, " +
                "BASIS_OF_PAY_CD, PERIOD_START_DT, PERIOD_END_DT, REGULAR_PAY_AMT, REGULAR_PAY_YTD_AMT, " +
                "OTHER_PAY_AMT, OTHER_PAY_YTD_AMT, BONUS_YTD_AMT, COMMISSION_YTD_AMT, OVERTIME_AMT, OVERTIME_YTD_AMT, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            var annualIncomeSql =
                $"INSERT INTO {AnnualIncomeTable} " +
                "(ANNUAL_INCOME_ID, EMPLOYMENT_DETAIL_ID, ANNUAL_INCOME_EXT_ID, REPORT_YEAR, REGULAR_AMT, " +
                "BONUS_AMT, COMMISSION_AMT, OVERTIME_AMT, OTHER_PAY_AMT, NET_PAY_AMT, GROSS_PAY_AMT, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            foreach (var truvEmployer in truvEmployers)
            {
                var placeholderEmploymentId = NewId();
                await ExecuteAsync(connection, transaction, placeholderEmploymentSql,
                    placeholderEmploymentId, verifiedId, truvEmployer.CompanyName, "ELECTRONICALLY_VERIFIED",
                    DataSource, DateTime.Today);

                var truvEmployerId = NewId();
                await ExecuteAsync(connection, transaction, truvEmployerSql,
                    truvEmployerId, placeholderEmploymentId, truvEmployer.Id, truvEmployer.ProductType,
                    truvEmployer.Status, truvEmployer.DataSource, truvEmployer.CompanyName,
                    ToYnChar(truvEmployer.IsSuspicious), DataSource, DateTime.Today);

                if (truvEmployer.Provider is { } provider)
                {
                    await ExecuteAsync(connection, transaction, truvProviderSql,
                        NewId(), truvEmployerId, provider.Id, provider.Name, provider.LogoUrl,
                        DataSource, DateTime.Today);
                }

                foreach (var detail in truvEmployer.Employments ?? Enumerable.Empty<TruvEmploymentDetailDTO>())
                {
                    var employmentDetailId = NewId();

                    await ExecuteAsync(connection, transaction, employmentDetailSql,
                        employmentDetailId, truvEmployerId, detail.EmploymentExternalId, detail.JobTitle,
                        detail.JobType, ToDbDate(detail.StartDate), ToDbDate(detail.EndDate),
                        ToYnChar(detail.IsActive), detail.AnnualIncome, detail.IncomeUnit, detail.PayRate,
                        detail.PayFrequency, DataSource, DateTime.Today);

                    if (detail.Profile is { } profile)
                    {
                        await ExecuteAsync(connection, transaction, employeeProfileSql,
                            NewId(), employmentDetailId, profile.EmployeeId, profile.FullName,
                            DataSource, DateTime.Today);
                    }

                    foreach (var statement in detail.Statements ?? Enumerable.Empty<TruvPayStatementDTO>())
                    {
                        await ExecuteAsync(connection, transaction, payStatementSql,
                            NewId(), employmentDetailId, statement.PayStatementExternalId, ToDbDate(statement.PayDate),
                            statement.NetPay, statement.NetPayYtd, statement.GrossPay, statement.GrossPayYtd,
                            statement.Bonus, statement.Commission, statement.Hours, statement.BasisOfPay,
                            ToDbDate(statement.PeriodStart), ToDbDate(statement.PeriodEnd), statement.Regular,
                            statement.RegularYtd, statement.OtherPay, statement.OtherPayYtd, statement.BonusYtd,
                            statement.CommissionYtd, statement.Overtime, statement.OvertimeYtd,
                            DataSource, DateTime.Today);
                    }

                    foreach (var annualIncome in detail.AnnualIncomeSummary ?? Enumerable.Empty<TruvAnnualIncomeDTO>())
                    {
                        await ExecuteAsync(connection, transaction, annualIncomeSql,
                            NewId(), employmentDetailId, annualIncome.AnnualIncomeExternalId, annualIncome.ReportYear,
                            annualIncome.Regular, annualIncome.Bonus, annualIncome.Commission, annualIncome.Overtime,
                            annualIncome.OtherPay, annualIncome.NetPay, annualIncome.GrossPay,
                            DataSource, DateTime.Today);
                    }
                }
            }
        }

        // ── MWRP_CE_REQUEST_ACK ────────────────────────────────────────────────────

        private static async Task InsertRequestAckAsync(
            TdConnection connection, TdTransaction transaction, long requestRowId, CepDWRequestDTO request)
        {
            // REQUEST_SEQUENCE_NUMBER is INTEGER here (unlike the VARCHAR column on MWRP_CE_REQUEST).
            object requestSequenceNumber = int.TryParse(request.RequestSequenceNumber, out var seqNum)
                ? seqNum
                : DBNull.Value;

            var sql =
                $"INSERT INTO {RequestAckTable} " +
                "(ACK_ROW_ID, REQUEST_ROW_ID, REQUEST_SEQUENCE_NUMBER, STATE_ID, REQUEST_SOURCE_CD, " +
                "ACKNOWLEDGEMENT_CD, PROCESSING_STATUS_CD, ACKNOWLEDGEMENT_MSG, NVH_REQUEST_ROW_ID, CREATED_TS, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            await ExecuteAsync(connection, transaction, sql,
                NewId(),
                requestRowId,
                requestSequenceNumber,
                request.State,
                request.RequestSource,
                "REQUEST_CREATED",
                "SUCCESS",
                "Request has been created successfully",
                requestRowId.ToString(),
                DateTime.UtcNow,
                DataSource,
                DateTime.Today);
        }

        // ── Helpers ────────────────────────────────────────────────────────────────

        private static async Task ExecuteAsync(
            TdConnection connection, TdTransaction transaction, string sql, params object?[] values)
        {
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
    }
}
