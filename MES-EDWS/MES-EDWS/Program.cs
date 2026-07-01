using Microsoft.AspNetCore.Authentication.Certificate;
using Microsoft.EntityFrameworkCore;
using MES_EDWS.Data;
using MES_EDWS.Models;
using MES_EDWS.Services;
using System.Security.Cryptography.X509Certificates;

var builder = WebApplication.CreateBuilder(args);

// Add MVC controllers with Razor views support
builder.Services.AddControllersWithViews();
builder.Services.AddOpenApi();

// TODO: Swap MockMedicalFrailtyService -> MedicalFrailtyService once Teradata connection details are available.
builder.Services.AddScoped<MES_EDWS.Services.IMedicalFrailtyService, MES_EDWS.Services.MockMedicalFrailtyService>();

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
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

// TODO: Re-enable certificate authentication once the client certificate is received.
// Configure Certificate Forwarding for reverse proxy scenarios
//builder.Services.AddCertificateForwarding(options =>
//{
//    options.CertificateHeader = "X-Client-Cert";
//    options.HeaderConverter = (headerValue) =>
//    {
//        X509Certificate2? clientCertificate = null;
//        if (!string.IsNullOrWhiteSpace(headerValue))
//        {
//            try
//            {
//                var certString = headerValue.Replace(" ", "").Replace("\n", "").Replace("\r", "");
//                if (certString.Contains("-----BEGIN"))
//                {
//                    certString = certString
//                        .Replace("-----BEGINCERTIFICATE-----", "")
//                        .Replace("-----ENDCERTIFICATE-----", "");
//                }
//                var certBytes = Convert.FromBase64String(certString);
//                clientCertificate = new X509Certificate2(certBytes);
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"Error parsing certificate from header: {ex.Message}");
//            }
//        }
//        return clientCertificate;
//    };
//});

// Configure Certificate Authentication
//builder.Services.AddAuthentication(CertificateAuthenticationDefaults.AuthenticationScheme)
//    .AddCertificate(options =>
//    {
//        options.RevocationMode = X509RevocationMode.NoCheck;
//        options.AllowedCertificateTypes = CertificateTypes.All;
//        options.Events = new CertificateAuthenticationEvents
//        {
//            OnCertificateValidated = context =>
//            {
//                var claims = new[]
//                {
//                    new System.Security.Claims.Claim(
//                        System.Security.Claims.ClaimTypes.NameIdentifier,
//                        context.ClientCertificate.Subject,
//                        System.Security.Claims.ClaimValueTypes.String,
//                        context.Options.ClaimsIssuer),
//                    new System.Security.Claims.Claim(
//                        System.Security.Claims.ClaimTypes.Name,
//                        context.ClientCertificate.Subject,
//                        System.Security.Claims.ClaimValueTypes.String,
//                        context.Options.ClaimsIssuer),
//                    new System.Security.Claims.Claim(
//                        "thumbprint",
//                        context.ClientCertificate.Thumbprint,
//                        System.Security.Claims.ClaimValueTypes.String,
//                        context.Options.ClaimsIssuer)
//                };
//                var identity = new System.Security.Claims.ClaimsIdentity(
//                    claims, CertificateAuthenticationDefaults.AuthenticationScheme);
//                context.Principal = new System.Security.Claims.ClaimsPrincipal(identity);
//                context.Success();
//                return Task.CompletedTask;
//            },
//            OnAuthenticationFailed = context =>
//            {
//                context.Fail($"Certificate authentication failed: {context.Exception.Message}");
//                return Task.CompletedTask;
//            }
//        };
//    });

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
            Username = "admin",
            PasswordHash = PasswordHelper.HashPassword("Admin123!")
        });
        auditDb.SaveChanges();
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Enable certificate forwarding middleware - must be before authentication
//app.UseCertificateForwarding();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=AuditLog}/{action=Index}/{id?}");

app.MapControllers();

app.Run();
