using Microsoft.EntityFrameworkCore;
using SC701.Models;

namespace SC701.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        public DbSet<Source> Sources { get; set; }
        public DbSet<SourceItem> SourceItems { get; set; }
    }
}
