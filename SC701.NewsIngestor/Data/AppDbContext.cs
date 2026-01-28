using Microsoft.EntityFrameworkCore;

namespace SC701.NewsIngestor.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // Los DbSet los agregamos en la próxima HU (Sources/SourceItems)
    }
}
