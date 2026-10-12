using MES_EDWS.Services;

var builder = WebApplication.CreateBuilder(args);

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
    });

builder.Services.AddScoped<MES_EDWS.Services.IMedicalFrailtyService, MES_EDWS.Services.MedicalFrailtyService>();
builder.Services.AddScoped<MES_EDWS.Services.IClientInfoService, MES_EDWS.Services.ClientInfoService>();

var app = builder.Build();

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
