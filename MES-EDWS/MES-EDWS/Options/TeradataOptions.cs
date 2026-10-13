namespace MES_EDWS.Options
{
    /// <summary>
    /// Bound from the "Teradata" section of appsettings.json. The per-environment file
    /// (appsettings.Development.json / appsettings.SIT.json / appsettings.UAT.json /
    /// appsettings.Production.json, selected by ASPNETCORE_ENVIRONMENT) overrides
    /// <see cref="Schema"/> so the same build targets the right database in each environment.
    /// </summary>
    public class TeradataOptions
    {
        public const string SectionName = "Teradata";

        /// <summary>
        /// Database/schema that owns the MWRP_CE_* and HR1_MEDICALLY_FRAIL_* tables, e.g.
        /// HR1_DMAS_POC (Dev), HR1_DMAS_SIT, HR1_DMAS_UAT, HR1_DMAS (Production).
        /// </summary>
        public string Schema { get; set; } = string.Empty;

        /// <summary>Returns "{Schema}.{tableName}" for use in SQL text.</summary>
        public string Table(string tableName) => $"{Schema}.{tableName}";
    }
}
