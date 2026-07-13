using MES_EDWS.Models;
using Teradata.Client.Provider;

namespace MES_EDWS.Services
{
    /// <summary>
    /// Persists CEP-ICD-003 CE verification result payloads to the HR1_MWR_* Teradata tables.
    /// The whole payload is written inside one transaction so a partially-saved request
    /// is never left behind.
    /// </summary>
    public class ClientInfoService : IClientInfoService
    {
        private readonly string _connectionString;
        private readonly ILogger<ClientInfoService> _logger;

        private const string DataSource = "MES-EDWS";

        // ── Table names ────────────────────────────────────────────────────────────
        private const string RequestTable          = "HR1_DMAS_POC.HR1_MWR_NVH_REQUEST_DEV";
        private const string IndividualTable       = "HR1_DMAS_POC.HR1_MWR_NVH_INDIVIDUAL_DEV";
        private const string ExemptionsTable       = "HR1_DMAS_POC.HR1_MWR_NVH_EXEMPTIONS_DEV";
        private const string RefDocumentsTable     = "HR1_DMAS_POC.HR1_MWR_REF_DOCUMENTS_DEV";
        private const string EmployerTable         = "HR1_DMAS_POC.HR1_MWR_EMPLOYER_DEV";
        private const string PayrollProviderTable  = "HR1_DMAS_POC.HR1_MWR_PAYROLL_PROVIDER_DEV";
        private const string EmploymentDetailTable = "HR1_DMAS_POC.HR1_MWR_EMPLOYMENT_DETAIL_DEV";
        private const string PayStatementTable     = "HR1_DMAS_POC.HR1_MWR_PAY_STATEMENT_DEV";
        private const string AnnualIncomeTable     = "HR1_DMAS_POC.HR1_MWR_ANNUAL_INCOME_DEV";
        private const string JobTrainingTable      = "HR1_DMAS_POC.HR1_MWR_JOB_TRAINING_DEV";
        private const string VolunteeringTable     = "HR1_DMAS_POC.HR1_MWR_VOLUNTEERING_DEV";
        private const string EnrollmentTable       = "HR1_DMAS_POC.HR1_MWR_ENROLLEMENT_DEV";
        private const string EnrollmentPeriodTable = "HR1_DMAS_POC.HR1_MWR_ENROLLEMENT_PERIOD_DEV";
        private const string EducationManualTable  = "HR1_DMAS_POC.HR1_MWR_EDUCATION_MANUAL_DEV";

        // ── Document categories used in HR1_MWR_REF_DOCUMENTS_DEV ──────────────────
        private const string DocCategoryExemption       = "EXEMPTION";
        private const string DocCategoryJobTraining     = "JOB_TRAINING";
        private const string DocCategoryVolunteering    = "VOLUNTEERING";
        private const string DocCategoryEducationManual = "EDUCATION_MANUAL";

        public ClientInfoService(IConfiguration configuration, ILogger<ClientInfoService> logger)
        {
            _connectionString = configuration.GetConnectionString("TeradataConnection")
                ?? throw new InvalidOperationException("TeradataConnection connection string is not configured.");
            _logger = logger;
        }

        // ── Entry point ────────────────────────────────────────────────────────────

        public async Task<string> SaveCeVerificationResultsAsync(CepDWRequestDTO request)
        {
            var nvhRequestId = GenerateNvhRequestId();

            await using var connection = new TdConnection(_connectionString);
            await connection.OpenAsync();

            await using var transaction = connection.BeginTransaction();

            try
            {
                await InsertRequestAsync(connection, transaction, nvhRequestId, request);

                var individualSeq = 0;
                foreach (var individual in request.NvhResponses)
                {
                    individualSeq++;
                    await InsertIndividualTreeAsync(
                        connection, transaction, nvhRequestId, request.RequestSequenceNumber,
                        individual, individualSeq);
                }

                transaction.Commit();

                _logger.LogInformation(
                    "CE verification results saved to Teradata. NvhRequestId: {NvhRequestId}, " +
                    "NvhRefferenceId: {NvhRefferenceId}, Individuals: {IndividualCount}",
                    nvhRequestId, request.NvhRefferenceId, request.NvhResponses.Count);

                return nvhRequestId;
            }
            catch (Exception ex)
            {
                try { transaction.Rollback(); }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx,
                        "Rollback failed after save error. NvhRequestId: {NvhRequestId}", nvhRequestId);
                }

                _logger.LogError(ex,
                    "Failed to save CE verification results. NvhRequestId: {NvhRequestId}, " +
                    "NvhRefferenceId: {NvhRefferenceId}",
                    nvhRequestId, request.NvhRefferenceId);
                throw;
            }
        }

        // ── HR1_MWR_NVH_REQUEST_DEV ────────────────────────────────────────────────

        private static async Task InsertRequestAsync(
            TdConnection connection, TdTransaction transaction,
            string nvhRequestId, CepDWRequestDTO request)
        {
            var sql =
                $"INSERT INTO {RequestTable} " +
                "(NVH_REQUEST_ID, NVH_REQUEST_SEQ_NUMB, NVH_REFERENCE_ID, STATE_ID, REQUEST_SOURCE, " +
                "REQUEST_TIME_STAMP, RECEIVED_TIME_STAMP, EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            await ExecuteAsync(connection, transaction, sql,
                nvhRequestId,
                request.RequestSequenceNumber,
                request.NvhRefferenceId,
                request.StateId,
                request.RequestSource,
                request.Timestamp,
                DateTime.UtcNow,
                DataSource,
                DateTime.Today);
        }

        // ── HR1_MWR_NVH_INDIVIDUAL_DEV and everything hanging off an individual ────

        private async Task InsertIndividualTreeAsync(
            TdConnection connection, TdTransaction transaction,
            string nvhRequestId, int requestSeqNumb,
            NvhIndividualResponseDTO individual, int individualSeq)
        {
            var individualId = NewId();
            var ceVerified   = individual.CeVerified;

            var sql =
                $"INSERT INTO {IndividualTable} " +
                "(NVH_INDIVIDUAL_ID, NVH_INDIVIDUAL_SEQ_NUM, NVH_REQUEST_ID, NVH_REQUEST_SEQ_NUMB, " +
                "NVH_INDV_REF_ID, EXEMPT_IND, COMPLAINT_IND, VERIFICATION_SOURCE, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            await ExecuteAsync(connection, transaction, sql,
                individualId,
                individualSeq,
                nvhRequestId,
                requestSeqNumb,
                individual.NvhIndvRefId.ToString(),
                ToYnChar(ceVerified.Exempt),
                ToYnChar(ceVerified.Complaint),
                ceVerified.Engagements?.VerificationSource,
                DataSource,
                DateTime.Today);

            if (ceVerified.Exemptions is { Count: > 0 })
                await InsertExemptionsAsync(connection, transaction, individualId, individualSeq, ceVerified.Exemptions);

            if (ceVerified.Engagements != null)
                await InsertEngagementsAsync(connection, transaction, individualId, individualSeq, ceVerified.Engagements);
        }

        // ── HR1_MWR_NVH_EXEMPTIONS_DEV ─────────────────────────────────────────────

        private async Task InsertExemptionsAsync(
            TdConnection connection, TdTransaction transaction,
            string individualId, int individualSeq, List<ExemptionResultDTO> exemptions)
        {
            var sql =
                $"INSERT INTO {ExemptionsTable} " +
                "(NVH_EXEMPTION_ID, NVH_EXEMPTION_SEQ_NUM, NVH_INDIVIDUAL_ID, NVH_INDIVIDUAL_SEQ_NUM, " +
                "CIRCUMSTANCE_CODE, CIRCUMSTANCE_DESC, START_DATE, END_DATE, ONGOING_PERMANENT_IND, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            var seq = 0;
            foreach (var exemption in exemptions)
            {
                seq++;
                var exemptionId = NewId();

                // The exempt=Y payload variant uses reason/exemptionStartDate/exemptionEndDate
                // instead of circumstanceCode/startDate/endDate — coalesce both shapes.
                await ExecuteAsync(connection, transaction, sql,
                    exemptionId,
                    seq,
                    individualId,
                    individualSeq,
                    exemption.CircumstanceCode ?? exemption.Reason,
                    exemption.CircumstanceDescription,
                    ToDbDate(exemption.StartDate ?? exemption.ExemptionStartDate),
                    ToDbDate(exemption.EndDate ?? exemption.ExemptionEndDate),
                    ToYnChar(exemption.OnGoingPermanent),
                    DataSource,
                    DateTime.Today);

                var documents = exemption.Documents ?? exemption.SupportingDocuments;
                await InsertDocumentsAsync(connection, transaction, DocCategoryExemption, exemptionId, documents);
            }
        }

        // ── Engagements: employment, job training, volunteering, education ─────────

        private async Task InsertEngagementsAsync(
            TdConnection connection, TdTransaction transaction,
            string individualId, int individualSeq, CeEngagementsDTO engagements)
        {
            if (engagements.Employment?.Employers is { Count: > 0 } employers)
                await InsertEmployersAsync(connection, transaction, individualId, individualSeq, employers);

            if (engagements.JobTraining is { Count: > 0 } jobTraining)
                await InsertJobTrainingAsync(connection, transaction, individualId, individualSeq, jobTraining);

            if (engagements.Volunteering is { Count: > 0 } volunteering)
                await InsertVolunteeringAsync(connection, transaction, individualId, individualSeq, volunteering);

            if (engagements.Education != null)
                await InsertEducationAsync(connection, transaction, individualId, individualSeq, engagements.Education);
        }

        // ── HR1_MWR_EMPLOYER_DEV / HR1_MWR_PAYROLL_PROVIDER_DEV ────────────────────

        private async Task InsertEmployersAsync(
            TdConnection connection, TdTransaction transaction,
            string individualId, int individualSeq, List<EmployerRecordDTO> employers)
        {
            var employerSql =
                $"INSERT INTO {EmployerTable} " +
                "(NVH_EMPLOYER_ID, NVH_EMPLOYER_SEQ_NUM, NVH_INDIVIDUAL_ID, NVH_INDIVIDUAL_SEQ_NUM, " +
                "TRUV_EMPLOYER_ID, PRODUCT_TYPE, STATUS, DATA_SOURCE, COMPANY_NAME, IS_SUSPICIOUS, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            var providerSql =
                $"INSERT INTO {PayrollProviderTable} " +
                "(PAYROLL_PROVIDER_ID, PAYROLL_PROVIDER_SEQ_NUM, NVH_EMPLOYER_ID, NVH_EMPLOYER_SEQ_NUM, " +
                "PAYROLL_PROVIDER_URL, EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, 'Y', ?, ?)";

            var employerSeq = 0;
            foreach (var employer in employers)
            {
                employerSeq++;
                var employerId = NewId();

                await ExecuteAsync(connection, transaction, employerSql,
                    employerId,
                    employerSeq,
                    individualId,
                    individualSeq,
                    employer.Id,
                    employer.ProductType,
                    employer.Status,
                    employer.DataSource,
                    employer.CompanyName,
                    ToYnChar(employer.IsSuspicious),
                    DataSource,
                    DateTime.Today);

                if (employer.Provider != null)
                {
                    await ExecuteAsync(connection, transaction, providerSql,
                        employer.Provider.Id ?? NewId(),
                        1,
                        employerId,
                        employerSeq,
                        employer.Provider.LogoUrl,
                        DataSource,
                        DateTime.Today);
                }

                if (employer.Employments is { Count: > 0 } employments)
                    await InsertEmploymentsAsync(connection, transaction, employerId, employerSeq, employments);
            }
        }

        // ── HR1_MWR_EMPLOYMENT_DETAIL_DEV and its children ─────────────────────────

        private async Task InsertEmploymentsAsync(
            TdConnection connection, TdTransaction transaction,
            string employerId, int employerSeq, List<EmploymentDetailDTO> employments)
        {
            var employmentSql =
                $"INSERT INTO {EmploymentDetailTable} " +
                "(NVH_EMPLOYMENT_ID, NVH_EMPLOYMENT_SEQ_NUM, NVH_EMPLOYER_ID, NVH_EMPLOYER_SEQ_NUM, " +
                "JOB_TITLE, JOB_TYPE, START_DATE, END_DATE, IS_ACTIVE, " +
                "INCOME, INCOME_UNIT, PAY_RATE, PAY_FREQUENCY, " +
                "EMP_FIRST_NAME, EMP_LAST_NAME, EMP_EMAIL, EMP_SSN, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            var statementSql =
                $"INSERT INTO {PayStatementTable} " +
                "(NVH_PAY_STATEMENT_ID, NVH_PAY_STATEMENT_SEQ_NUM, NVH_EMPLOYMENT_ID, NVH_EMPLOYMENT_SEQ_NUM, " +
                "PAY_DATE, GROSS_PAY, NET_PAY, EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            var annualIncomeSql =
                $"INSERT INTO {AnnualIncomeTable} " +
                "(NVH_ANNUAL_INCOME_ID, NVH_ANNUAL_INCOME_SEQ_NUM, NVH_EMPLOYMENT_ID, NVH_EMPLOYMENT_SEQ_NUM, " +
                "INCOME_YEAR, INCOME_AMOUNT, EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            var employmentSeq = 0;
            foreach (var employment in employments)
            {
                employmentSeq++;
                var employmentId = NewId();

                await ExecuteAsync(connection, transaction, employmentSql,
                    employmentId,
                    employmentSeq,
                    employerId,
                    employerSeq,
                    employment.JobTitle,
                    employment.JobType,
                    ToDbDate(employment.StartDate),
                    ToDbDate(employment.EndDate),
                    ToYnChar(employment.IsActive),
                    employment.Income,
                    employment.IncomeUnit,
                    employment.PayRate,
                    employment.PayFrequency,
                    employment.Profile?.FirstName,
                    employment.Profile?.LastName,
                    employment.Profile?.Email,
                    employment.Profile?.Ssn,
                    DataSource,
                    DateTime.Today);

                var statementSeq = 0;
                foreach (var statement in employment.Statements ?? Enumerable.Empty<PayStatementDTO>())
                {
                    statementSeq++;
                    await ExecuteAsync(connection, transaction, statementSql,
                        NewId(),
                        statementSeq,
                        employmentId,
                        employmentSeq,
                        ToDbDate(statement.PayDate),
                        statement.GrossPay,
                        statement.NetPay,
                        DataSource,
                        DateTime.Today);
                }

                var annualIncomeSeq = 0;
                foreach (var annualIncome in employment.AnnualIncomeSummary ?? Enumerable.Empty<AnnualIncomeDTO>())
                {
                    annualIncomeSeq++;
                    await ExecuteAsync(connection, transaction, annualIncomeSql,
                        NewId(),
                        annualIncomeSeq,
                        employmentId,
                        employmentSeq,
                        annualIncome.Year?.ToString(),
                        annualIncome.Income,
                        DataSource,
                        DateTime.Today);
                }
            }
        }

        // ── HR1_MWR_JOB_TRAINING_DEV ───────────────────────────────────────────────

        private async Task InsertJobTrainingAsync(
            TdConnection connection, TdTransaction transaction,
            string individualId, int individualSeq, List<JobTrainingResultDTO> jobTrainings)
        {
            // END-DATE is a quoted identifier in the table DDL, so it must stay quoted here.
            var sql =
                $"INSERT INTO {JobTrainingTable} " +
                "(NVH_JOB_TRAINING_ID, NVH_JOB_TRAINING_SEQ_NUM, NVH_INDIVIDUAL_ID, NVH_INDIVIDUAL_SEQ_NUM, " +
                "ORGANIZATION_ID, ORGANIZATION_NAME, TRAINING_HOURS, START_DATE, \"END-DATE\", EFFECTIVE_PERIOD, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            var seq = 0;
            foreach (var training in jobTrainings)
            {
                seq++;
                var trainingId = NewId();

                await ExecuteAsync(connection, transaction, sql,
                    trainingId,
                    seq,
                    individualId,
                    individualSeq,
                    training.OrganizationId,
                    training.OrganizationName,
                    training.Hours?.ToString(),
                    ToDbDate(training.StartDate),
                    ToDbDate(training.EndDate),
                    training.EffectivePeriod,
                    DataSource,
                    DateTime.Today);

                await InsertDocumentsAsync(connection, transaction, DocCategoryJobTraining, trainingId, training.Documents);
            }
        }

        // ── HR1_MWR_VOLUNTEERING_DEV ───────────────────────────────────────────────

        private async Task InsertVolunteeringAsync(
            TdConnection connection, TdTransaction transaction,
            string individualId, int individualSeq, List<VolunteeringResultDTO> volunteerings)
        {
            // END-DATE is a quoted identifier in the table DDL, so it must stay quoted here.
            var sql =
                $"INSERT INTO {VolunteeringTable} " +
                "(NVH_VOLUNTEERING_ID, NVH_VOLUNTEERING_SEQ_NUM, NVH_INDIVIDUAL_ID, NVH_INDIVIDUAL_SEQ_NUM, " +
                "ORGANIZATION_ID, ORGANIZATION_NAME, VOLUNTEER_HOURS, START_DATE, \"END-DATE\", EFFECTIVE_PERIOD, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            var seq = 0;
            foreach (var volunteering in volunteerings)
            {
                seq++;
                var volunteeringId = NewId();

                await ExecuteAsync(connection, transaction, sql,
                    volunteeringId,
                    seq,
                    individualId,
                    individualSeq,
                    volunteering.OrganizationId,
                    volunteering.OrganizationName,
                    volunteering.Hours?.ToString(),
                    ToDbDate(volunteering.StartDate),
                    ToDbDate(volunteering.EndDate),
                    volunteering.EffectivePeriod,
                    DataSource,
                    DateTime.Today);

                await InsertDocumentsAsync(connection, transaction, DocCategoryVolunteering, volunteeringId, volunteering.Documents);
            }
        }

        // ── HR1_MWR_ENROLLEMENT_DEV / HR1_MWR_ENROLLEMENT_PERIOD_DEV /
        //    HR1_MWR_EDUCATION_MANUAL_DEV ──────────────────────────────────────────

        private async Task InsertEducationAsync(
            TdConnection connection, TdTransaction transaction,
            string individualId, int individualSeq, EducationVerifiedDTO education)
        {
            var enrollmentSql =
                $"INSERT INTO {EnrollmentTable} " +
                "(NVH_ENROLLEMENT_ID, NVH_ENROLLEMENT_SEQ_NUM, NVH_INDIVIDUAL_ID, NVH_INDIVIDUAL_SEQ_NUM, " +
                "ELECTRONIC_SOURCE, OFFICIAL_SCHOOL_NAME, SCHOOL_CODE, BRANCH_CODE, CURRENT_ENRL_STATUS, " +
                "EFFECTIVE_PERIOD, EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            var periodSql =
                $"INSERT INTO {EnrollmentPeriodTable} " +
                "(NVH_ENROLLEMENT_PERIOD_ID, NVH_ENROLLEMENT_PERIOD_SEQ_NUM, NVH_ENROLLEMENT_ID, NVH_ENROLLEMENT_SEQ_NUM, " +
                "TERM_START_DATE, TERM_END_DATE, ANTICIPATED_GRAD_DATE, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            var manualSql =
                $"INSERT INTO {EducationManualTable} " +
                "(NVH_EDUCATION_MANUAL_ID, NVH_EDUCATION_MANUAL_SEQ_NUM, NVH_INDIVIDUAL_ID, NVH_INDIVIDUAL_SEQ_NUM, " +
                "SCHOOL_NAME, ENROLLEMENT_STATUS, START_DATE, END_DATE, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            var enrollmentSeq = 0;
            foreach (var enrollment in education.ElectronicallyVerifiedData ?? Enumerable.Empty<NscEnrollmentDTO>())
            {
                enrollmentSeq++;
                var enrollmentId = NewId();

                await ExecuteAsync(connection, transaction, enrollmentSql,
                    enrollmentId,
                    enrollmentSeq,
                    individualId,
                    individualSeq,
                    enrollment.ElectronicSource,
                    enrollment.OfficialSchoolName,
                    enrollment.SchoolCode,
                    enrollment.BranchCode,
                    enrollment.CurrentEnrollmentStatus,
                    null,   // EFFECTIVE_PERIOD is not present in the NSC payload
                    DataSource,
                    DateTime.Today);

                var periodSeq = 0;
                foreach (var period in enrollment.EnrollmentData ?? Enumerable.Empty<EnrollmentPeriodDTO>())
                {
                    periodSeq++;
                    await ExecuteAsync(connection, transaction, periodSql,
                        NewId(),
                        periodSeq,
                        enrollmentId,
                        enrollmentSeq,
                        ToDbDate(period.TermStartDate),
                        ToDbDate(period.TermEndDate),
                        ToDbDate(period.AnticipatedGraduationDate),
                        DataSource,
                        DateTime.Today);
                }
            }

            var manualSeq = 0;
            foreach (var manual in education.NonElectronicallyVerifiedData ?? Enumerable.Empty<EducationManualDTO>())
            {
                manualSeq++;
                var manualId = NewId();

                await ExecuteAsync(connection, transaction, manualSql,
                    manualId,
                    manualSeq,
                    individualId,
                    individualSeq,
                    manual.SchoolName,
                    manual.EnrollmentStatus,
                    ToDbDate(manual.StartDate),
                    ToDbDate(manual.EndDate),
                    DataSource,
                    DateTime.Today);

                await InsertDocumentsAsync(connection, transaction, DocCategoryEducationManual, manualId, manual.Documents);
            }
        }

        // ── HR1_MWR_REF_DOCUMENTS_DEV ──────────────────────────────────────────────

        /// <summary>
        /// Documents from any part of the payload land in one reference table.
        /// DOCUMENT_CATEGORY records where the document came from and
        /// DOCUMENT_LINK_KEY holds the generated id of the owning row.
        /// </summary>
        private async Task InsertDocumentsAsync(
            TdConnection connection, TdTransaction transaction,
            string category, string linkKey, List<DocumentRefDTO>? documents)
        {
            if (documents == null || documents.Count == 0)
                return;

            var sql =
                $"INSERT INTO {RefDocumentsTable} " +
                "(DOCUMENT_ID, DOCUMENT_SEQ_NUM, DOCUMENT_CATEGORY, DOCUMENT_LINK_KEY, DOCUMENT_TYPE, FILE_NAME, " +
                "EDWS_CURRENT_IND, EDWS_DATASOURCE, EDWS_DATE_INSERT) " +
                "VALUES (?, ?, ?, ?, ?, ?, 'Y', ?, ?)";

            var seq = 0;
            foreach (var document in documents)
            {
                seq++;
                await ExecuteAsync(connection, transaction, sql,
                    NewId(),
                    seq,
                    category,
                    linkKey,
                    document.DocumentType,
                    document.FileName,
                    DataSource,
                    DateTime.Today);
            }
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

        /// <summary>Generated ids are GUID strings; every *_ID column is VARCHAR(50).</summary>
        private static string NewId() => Guid.NewGuid().ToString("N");

        /// <summary>
        /// Short numeric NVH request id matching the sample format in the ICD (e.g. "988862").
        /// </summary>
        private static string GenerateNvhRequestId() =>
            Random.Shared.Next(100_000, 999_999).ToString();

        /// <summary>Normalises "Y"/"Yes"/"true" style payload values to a single Y/N character.</summary>
        private static object ToYnChar(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return DBNull.Value;
            var upper = value.Trim().ToUpperInvariant();
            return upper is "Y" or "YES" or "TRUE" or "1" ? "Y" : "N";
        }

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
