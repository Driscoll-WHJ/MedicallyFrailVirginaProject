using Microsoft.AspNetCore.Authentication.Certificate;
using Microsoft.EntityFrameworkCore;
using MES_EDWS.Data;
using MES_EDWS.Models;
using MES_EDWS.Services;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddScoped<MES_EDWS.Services.IMedicalFrailtyService, MES_EDWS.Services.MedicalFrailtyService>();
builder.Services.AddScoped<MES_EDWS.Services.IClientInfoService, MES_EDWS.Services.ClientInfoService>();

// Configure SQLite Audit Database
builder.Services.AddDbContext<AuditDbContext>(options =>
{
    var dbPath = builder.Configuration.GetConnectionString("AuditSqlite")
                 ?? "Data Source=audit.db";
    options.UseSqlite(dbPath);
});

// Load the vendor-supplied DataPower SIT certificate chain from the SIT_DPCerts folder.
// These certs authenticate inbound calls from DataPower (datapower.sit.va.healthinteractive.net).
var certsFolder = Path.Combine(AppContext.BaseDirectory, "SIT_DPCerts");
var rootCert         = X509Certificate2.CreateFromPemFile(Path.Combine(certsFolder, "Root.txt"));
var intermediateCert = X509Certificate2.CreateFromPemFile(Path.Combine(certsFolder, "Intermediate.txt"));

// DataPower forwards the client cert as an inline PEM in the X-Client-Cert header:
//   X-Client-Cert: -----BEGIN CERTIFICATE-----<base64>-----END CERTIFICATE-----
// Note: there are no line breaks inside the base64 block; CreateFromPem handles this correctly.
builder.Services.AddCertificateForwarding(options =>
{
    options.CertificateHeader = "X-Client-Cert";
    options.HeaderConverter = (headerValue) =>
    {
        if (string.IsNullOrWhiteSpace(headerValue))
            return null!;

        try
        {
            var pem = headerValue.Trim();

            if (pem.Contains("-----BEGIN CERTIFICATE-----"))
                return X509Certificate2.CreateFromPem(pem);

            // Fallback: treat as raw base64 DER bytes (no PEM markers)
            return X509CertificateLoader.LoadCertificate(Convert.FromBase64String(pem));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CertForwarding] Failed to parse X-Client-Cert header: {ex.Message}");
            return null!;
        }
    };
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
    })
    // Certificate authentication for the DataPower API caller
    .AddCertificate(options =>
    {
        options.RevocationMode        = X509RevocationMode.NoCheck;
        options.AllowedCertificateTypes = CertificateTypes.All;
        options.Events = new CertificateAuthenticationEvents
        {
            OnCertificateValidated = context =>
            {
                var cert    = context.ClientCertificate;
                var request = context.HttpContext.Request;

                // DataPower also sends the parsed subject DN as a convenience header.
                // Cross-validate it against the actual certificate subject so that any
                // mismatch (misconfiguration or header spoofing attempt) is caught early.
                var headerDn = request.Headers["X-Client-Subject-DN"].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(headerDn))
                {
                    // X509 subjects use RFC 2253 comma ordering; normalise both sides for comparison.
                    var certSubjectNorm   = cert.Subject.Replace(" ", "");
                    var headerSubjectNorm = headerDn.Replace(" ", "");

                    if (!certSubjectNorm.Equals(headerSubjectNorm, StringComparison.OrdinalIgnoreCase))
                    {
                        context.Fail(
                            $"X-Client-Subject-DN header '{headerDn}' does not match " +
                            $"certificate Subject '{cert.Subject}'.");
                        return Task.CompletedTask;
                    }
                }

                // Build and validate the chain against the vendor-supplied CA certs only.
                // CustomRootTrust prevents falling back to the machine trust store.
                using var chain = new X509Chain();
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                chain.ChainPolicy.TrustMode      = X509ChainTrustMode.CustomRootTrust;
                chain.ChainPolicy.CustomTrustStore.Add(rootCert);
                chain.ChainPolicy.ExtraStore.Add(intermediateCert);

                if (!chain.Build(cert))
                {
                    var errors = string.Join("; ", chain.ChainStatus.Select(s => s.StatusInformation.Trim()));
                    context.Fail($"Certificate chain validation failed: {errors}");
                    return Task.CompletedTask;
                }

                // Confirm the certificate belongs to the expected DataPower SIT endpoint.
                const string expectedCn = "datapower.sit.va.healthinteractive.net";
                if (!cert.Subject.Contains(expectedCn, StringComparison.OrdinalIgnoreCase))
                {
                    context.Fail($"Certificate subject '{cert.Subject}' does not contain the expected CN '{expectedCn}'.");
                    return Task.CompletedTask;
                }

                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, cert.Subject,    ClaimValueTypes.String, context.Options.ClaimsIssuer),
                    new Claim(ClaimTypes.Name,           cert.Subject,    ClaimValueTypes.String, context.Options.ClaimsIssuer),
                    new Claim("thumbprint",              cert.Thumbprint, ClaimValueTypes.String, context.Options.ClaimsIssuer),
                    new Claim("subject-dn",              cert.Subject,    ClaimValueTypes.String, context.Options.ClaimsIssuer),
                };

                var identity = new ClaimsIdentity(claims, CertificateAuthenticationDefaults.AuthenticationScheme);
                context.Principal = new ClaimsPrincipal(identity);
                context.Success();
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                context.Fail($"Certificate authentication failed: {context.Exception.Message}");
                return Task.CompletedTask;
            }
        };
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

// Certificate forwarding must run before authentication so the cert is available
app.UseCertificateForwarding();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=AuditLog}/{action=Index}/{id?}");

app.MapControllers();

app.Run();
