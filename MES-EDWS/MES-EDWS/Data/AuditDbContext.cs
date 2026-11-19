using Microsoft.EntityFrameworkCore;
using MES_EDWS.Models;

namespace MES_EDWS.Data
{
    public class AuditDbContext : DbContext
    {
        public AuditDbContext(DbContextOptions<AuditDbContext> options)
            : base(options)
        {
        }

        public DbSet<MedicalFrailtyAuditLog> MedicalFrailtyAuditLogs { get; set; }
        public DbSet<AppUser> AppUsers { get; set; }
    }
}
