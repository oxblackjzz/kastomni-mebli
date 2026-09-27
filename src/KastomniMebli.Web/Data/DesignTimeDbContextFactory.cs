using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace KastomniMebli.Web.Data;

/// <summary>
/// Для `dotnet ef migrations add` — щоб створювати міграції без живої бази.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=kastomni_mebli;Username=postgres")
            .Options;
        return new AppDbContext(options);
    }
}
