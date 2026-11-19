using MES_EDWS.Controllers;
using MES_EDWS.Models;
using MES_EDWS.Options;
using MES_EDWS.Services;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

var builder = WebApplication.CreateBuilder(args);

// ── Environment-specific configuration ──────────────────────────────────────────
// WebApplication.CreateBuilder already loads appsettings.json and then
// appsettings.{ASPNETCORE_ENVIRONMENT}.json on top of it, so Development / SIT / UAT /
// Production each get their own Teradata:Schema (and optionally their own connection
// string) without any code change. ValidateOnStart makes a missing/blank schema a
// startup failure rather than a runtime SQL error against the wrong database.
builder.Services
    .AddOptions<TeradataOptions>()
    .Bind(builder.Configuration.GetSection(TeradataOptions.SectionName))
    .Validate(o => !string.IsNullOrWhiteSpace(o.Schema),
        $"{TeradataOptions.SectionName}:{nameof(TeradataOptions.Schema)} is not configured. " +
        "Check ASPNETCORE_ENVIRONMENT and the matching appsettings.{Environment}.json.")
    .ValidateOnStart();

builder.Services
    .AddOptions<DeploymentOptions>()
    .Bind(builder.Configuration.GetSection(DeploymentOptions.SectionName))
    .Validate(o => Uri.TryCreate(o.BaseUrl, UriKind.Absolute, out var url) &&
        (url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeHttp) &&
        string.IsNullOrEmpty(url.Query) && string.IsNullOrEmpty(url.Fragment) &&
        string.IsNullOrEmpty(url.UserInfo),
        "Deployment:BaseUrl must be an absolute HTTP(S) URL without credentials, a query, or a fragment. " +
        "Check ASPNETCORE_ENVIRONMENT and the matching appsettings.{Environment}.json.")
    .ValidateOnStart();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // The CEP-ICD-003 2026-08 payload sends some Truv/payroll numeric values
        // (e.g. income, netPay, hours) as quoted JSON strings. Allow those to bind
        // to decimal?/int? DTO properties without a custom converter.
        options.JsonSerializerOptions.NumberHandling =
            System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString;

        // docs/SamplePayload.json uses inconsistent casing for some fields across the
        // payload (e.g. "updateSw" vs. the DTOs' historical "updatesw" attribute,
        // "organizationID" vs. "organizationId"). Case-insensitive matching lets all of
        // these bind correctly without hunting down every casing variant by hand.
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;

        // CEP-003 required elements may be sent as "" even when they are bool/number/date fields.
        options.JsonSerializerOptions.Converters.Add(new EmptyStringAsNullConverterFactory());
    });

builder.Services.AddSwaggerGen();
builder.Services.AddOptions<SwaggerGenOptions>()
    .Configure<IOptions<DeploymentOptions>>((swagger, deployment) =>
    {
        swagger.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "MES-EDWS API",
            Version = "v1"
        });
        swagger.AddServer(new OpenApiServer
        {
            Url = deployment.Value.BaseUrl!.TrimEnd('/'),
            Description = builder.Environment.EnvironmentName
        });
    });

builder.Services.AddScoped<IMedicalFrailtyService, MedicalFrailtyService>();
builder.Services.AddScoped<IClientInfoService, ClientInfoService>();

var app = builder.Build();

// Log the resolved environment on startup so a mis-deployed build is obvious in the logs.
{
    var teradata   = app.Services.GetRequiredService<IOptions<TeradataOptions>>().Value;
    var deployment = app.Services.GetRequiredService<IOptions<DeploymentOptions>>().Value;
    app.Logger.LogInformation(
        "MES-EDWS starting. Environment: {Environment}, BaseUrl: {BaseUrl}, Teradata schema: {Schema}",
        app.Environment.EnvironmentName, deployment.BaseUrl, teradata.Schema);
}

app.UseHttpsRedirection();

// MedicallyFrailController / ClientInfoController store the raw request JSON in
// MWRP_CE_MED_FRAIL_API_REQUEST / MWRP_CE_API_REQUEST, which requires re-reading the body
// after model binding.
app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/" + MedicallyFrailController.ApiRoute) ||
               context.Request.Path.StartsWithSegments("/" + ClientInfoController.ApiRoute),
    branch => branch.Use((context, next) =>
    {
        context.Request.EnableBuffering();
        return next(context);
    }));

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    // Relative to /swagger/ so IIS application paths such as /SIT are preserved.
    options.SwaggerEndpoint("v1/swagger.json", "MES-EDWS API v1");
});

app.MapControllers();

app.Run();
