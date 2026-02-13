using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SC701.Models;

namespace SC701.Data
{
    /// <summary>
    /// DbContext actualizado con Identity y nuevas tablas
    /// HU-08: Settings/Secrets
    /// HU-09: Identity para autenticación
    /// </summary>
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        // Tablas existentes
        public DbSet<Source> Sources { get; set; }
        public DbSet<SourceItem> SourceItems { get; set; }

        // HU-08: Nuevas tablas para Settings y Secrets
        public DbSet<Setting> Settings { get; set; }
        public DbSet<Secret> Secrets { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configuraciones adicionales si son necesarias
            builder.Entity<Setting>()
                .HasIndex(s => s.Key)
                .IsUnique();

            builder.Entity<Secret>()
                .HasOne(s => s.Source)
                .WithMany()
                .HasForeignKey(s => s.SourceId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}