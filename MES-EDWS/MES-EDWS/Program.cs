using Microsoft.EntityFrameworkCore;
using MES_EDWS.Data;
using MES_EDWS.Models;
using MES_EDWS.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

// Swagger is enabled in all environments (including IIS/Production) so the API docs
// remain available when hosted; restrict access at the network/firewall level if needed.
app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=AuditLog}/{action=Index}/{id?}");

app.MapControllers();

app.Run();
