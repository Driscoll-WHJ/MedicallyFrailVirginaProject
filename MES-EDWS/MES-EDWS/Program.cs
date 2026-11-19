using Microsoft.EntityFrameworkCore;
using MES_EDWS.Data;
using MES_EDWS.Models;
using MES_EDWS.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews()
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

// Configure SQLite Audit Database
builder.Services.AddDbContext<AuditDbContext>(options =>
{
    var dbPath = builder.Configuration.GetConnectionString("AuditSqlite")
                 ?? "Data Source=audit.db";
    options.UseSqlite(dbPath);
});

// Cookie authentication for the web UI (login screen / audit log viewer)
builder.Services.AddAuthentication("CookieAuth")
    .AddCookie("CookieAuth", options =>
    {
        options.LoginPath        = "/Account/Login";
        options.LogoutPath       = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan   = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Ensure the SQLite audit database, tables, and default admin user exist on startup
using (var scope = app.Services.CreateScope())
{
    var auditDb = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
    auditDb.Database.EnsureCreated();

    if (!auditDb.AppUsers.Any())
    {
        auditDb.AppUsers.Add(new AppUser
        {
            Username     = "admin",
            PasswordHash = PasswordHelper.HashPassword("Admin123!")
        });
        auditDb.SaveChanges();
    }
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=AuditLog}/{action=Index}/{id?}");

app.MapControllers();

app.Run();
