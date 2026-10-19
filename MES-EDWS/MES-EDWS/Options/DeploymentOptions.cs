namespace MES_EDWS.Options
{
    /// <summary>
    /// Bound from the "Deployment" section of appsettings.json. Used for startup logging
    /// and the public server URL in Swagger, including any application path such as /SIT.
    /// </summary>
    public class DeploymentOptions
    {
        public const string SectionName = "Deployment";

        /// <summary>Public base URL this deployment is served from, e.g. https://vaedws-dev.optum.com/SIT.</summary>
        public string? BaseUrl { get; set; }
    }
}
