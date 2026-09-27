using System.Text.RegularExpressions;
using KastomniMebli.Web.Leads;
using Microsoft.EntityFrameworkCore;

namespace KastomniMebli.Web.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Lead> Leads => Set<Lead>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Lead>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(LeadForm.NameMax).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(13).IsRequired();
            e.Property(x => x.PhoneRaw).HasMaxLength(LeadForm.PhoneRawMax).IsRequired();
            e.Property(x => x.Location).HasMaxLength(LeadForm.LocationMax);
            e.Property(x => x.Dimensions).HasMaxLength(LeadForm.DimensionsMax);
            e.Property(x => x.Comment).HasMaxLength(LeadForm.CommentMax);
            e.Property(x => x.Source).HasMaxLength(32).IsRequired();
            e.Property(x => x.Status).HasMaxLength(32).IsRequired();
            e.Property(x => x.IpHash).HasMaxLength(16);
            e.Property(x => x.UserAgent).HasMaxLength(512);
            e.Property(x => x.TelegramError).HasMaxLength(500);

            e.HasIndex(x => x.CreatedAt);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.Phone);
        });

        ApplySnakeCase(b);
    }

    // Як у MES: таблиці й колонки в snake_case, щоб зручно писати SQL руками.
    private static void ApplySnakeCase(ModelBuilder b)
    {
        foreach (var entity in b.Model.GetEntityTypes())
        {
            entity.SetTableName(ToSnake(entity.GetTableName()!));
            foreach (var p in entity.GetProperties())
                p.SetColumnName(ToSnake(p.Name));
            foreach (var key in entity.GetKeys())
                key.SetName(ToSnake(key.GetName()!));
            foreach (var index in entity.GetIndexes())
                index.SetDatabaseName(ToSnake(index.GetDatabaseName()!));
        }
    }

    private static string ToSnake(string name) =>
        Regex.Replace(name, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant();
}
