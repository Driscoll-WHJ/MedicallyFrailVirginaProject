namespace MES_EDWS.Models
{
    /// <summary>
    /// Maps to HR1_DMAS_POC.HR1_MEDICALLY_FRAIL_MEMBERS — the lookup table
    /// queried by MMIS_ENROLLEE_ID (primary) or SSN (fallback).
    /// </summary>
    public class MedicalFrailtyRecord
    {
        /// <summary>MMIS_ENROLLEE_ID VARCHAR(15)</summary>
        public string? MmisEnrolleeId { get; set; }

        /// <summary>SSN VARCHAR(10)</summary>
        public string? Ssn { get; set; }

        /// <summary>MEDICALLY_FRAIL_FLAG CHAR(1) — 'Y' maps to true.</summary>
        public bool MedicallyFrail { get; set; }

        /// <summary>CIRCUMSTANCE_START_DATE DATE</summary>
        public string? CircumstanceStartDate { get; set; }

        /// <summary>CIRCUMSTANCE_END_DATE DATE — null if ongoing.</summary>
        public string? CircumstanceEndDate { get; set; }

        /// <summary>EDWS_CURRENT_IND CHAR(1) — always 'Y' in query results.</summary>
        public string? EdwsCurrentInd { get; set; }

        /// <summary>EDWS_DATASOURCE VARCHAR(100)</summary>
        public string? EdwsDatasource { get; set; }

        /// <summary>EDWS_DATE_INSERT DATE</summary>
        public string? EdwsDateInsert { get; set; }
    }
}
