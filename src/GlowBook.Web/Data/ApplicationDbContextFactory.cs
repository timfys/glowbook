using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GlowBook.Web.Data;

/// <summary>Design-time factory: EF migrations are generated for PostgreSQL.</summary>
public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=5432;Database=railway;Username=postgres;Password=postgres")
            .Options;
        return new ApplicationDbContext(options);
    }
}
