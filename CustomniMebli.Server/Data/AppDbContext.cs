using Microsoft.EntityFrameworkCore;
using CustomniMebli.Server.Models;

namespace CustomniMebli.Server.Data;

public class AppDbContext : DbContext
{
  public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<PortfolioItem> PortfolioItems => Set<PortfolioItem>();
 public DbSet<ContactRequest> ContactRequests => Set<ContactRequest>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<SiteSettings> SiteSettings => Set<SiteSettings>();

 protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
  base.OnModelCreating(modelBuilder);
        // Data will be seeded in Program.cs
    }
}
