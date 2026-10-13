namespace MES_EDWS.Options
{
    /// <summary>
    /// Bound from the "Deployment" section of appsettings.json. Informational only — used for
    /// startup logging and the /status endpoint so a deployment can be checked against the
    /// environment it was published to.
    /// </summary>
    public class DeploymentOptions
    {
        public const string SectionName = "Deployment";

        /// <summary>Public base URL this deployment is served from, e.g. https://vaedws-dev.optum.com/SIT.</summary>
        public string? BaseUrl { get; set; }
    }
}
