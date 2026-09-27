using System.Text.RegularExpressions;
using KastomniMebli.Web.Leads;
using Microsoft.EntityFrameworkCore;

namespace KastomniMebli.Web.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderAssignee> OrderAssignees => Set<OrderAssignee>();
    public DbSet<OrderStatusChange> OrderStatusHistory => Set<OrderStatusChange>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<OrderShare> OrderShares => Set<OrderShare>();
    public DbSet<ShareTemplate> ShareTemplates => Set<ShareTemplate>();
    public DbSet<OrderFile> OrderFiles => Set<OrderFile>();
    public DbSet<AppSetting> Settings => Set<AppSetting>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
    public DbSet<PortfolioWork> PortfolioWorks => Set<PortfolioWork>();
    public DbSet<PortfolioPhoto> PortfolioPhotos => Set<PortfolioPhoto>();

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

        b.Entity<User>(e =>
        {
            e.Property(x => x.Login).HasMaxLength(40).IsRequired();
            e.Property(x => x.DisplayName).HasMaxLength(80).IsRequired();
            e.Property(x => x.Role).HasMaxLength(16).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(200).IsRequired();
            e.Property(x => x.SecurityStamp).HasMaxLength(64).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(13);
            e.Property(x => x.TelegramChatId).HasMaxLength(32);
            e.HasIndex(x => x.Login).IsUnique();
        });

        b.Entity<Client>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(13);
            e.Property(x => x.Address).HasMaxLength(300);
            e.Property(x => x.Telegram).HasMaxLength(40);
            e.Property(x => x.Source).HasMaxLength(32).IsRequired();
            e.Property(x => x.Note).HasMaxLength(2000);
            e.HasIndex(x => x.Phone).IsUnique();
        });

        b.Entity<Order>(e =>
        {
            e.Property(x => x.Number).HasMaxLength(16).IsRequired();
            e.Property(x => x.Kind).HasMaxLength(16).IsRequired();
            e.Property(x => x.Status).HasMaxLength(32).IsRequired();
            e.Property(x => x.Source).HasMaxLength(32).IsRequired();
            e.Property(x => x.Address).HasMaxLength(300);
            e.Property(x => x.Comment).HasMaxLength(4000);
            e.Property(x => x.ContractAmount).HasPrecision(12, 2);
            e.HasIndex(x => x.Number).IsUnique();
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.Kind);
            e.HasIndex(x => x.LeadId).IsUnique();
            e.HasOne<Lead>().WithMany().HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Client).WithMany(c => c.Orders).HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<OrderAssignee>(e =>
        {
            e.Property(x => x.Role).HasMaxLength(16).IsRequired();
            e.HasIndex(x => new { x.OrderId, x.UserId, x.Role }).IsUnique();
            e.HasOne<Order>().WithMany(o => o.Assignees).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<OrderStatusChange>(e =>
        {
            e.Property(x => x.FromStatus).HasMaxLength(32);
            e.Property(x => x.ToStatus).HasMaxLength(32).IsRequired();
            e.HasOne<Order>().WithMany(o => o.StatusHistory).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Payment>(e =>
        {
            e.Property(x => x.Kind).HasMaxLength(16).IsRequired();
            e.Property(x => x.Method).HasMaxLength(16).IsRequired();
            e.Property(x => x.Amount).HasPrecision(12, 2);
            e.Property(x => x.Note).HasMaxLength(500);
            e.HasIndex(x => x.PaidOn);
            e.HasOne<Order>().WithMany(o => o.Payments).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Expense>(e =>
        {
            e.Property(x => x.Category).HasMaxLength(16).IsRequired();
            e.Property(x => x.Amount).HasPrecision(12, 2);
            e.Property(x => x.Note).HasMaxLength(500);
            e.HasIndex(x => x.SpentOn);
            e.HasOne<Order>().WithMany(o => o.Expenses).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<OrderShare>(e =>
        {
            e.Property(x => x.Basis).HasMaxLength(24).IsRequired();
            e.Property(x => x.Value).HasPrecision(12, 2);
            e.Property(x => x.PaidAmount).HasPrecision(12, 2);
            e.HasOne<Order>().WithMany(o => o.Shares).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<ShareTemplate>(e =>
        {
            e.Property(x => x.Basis).HasMaxLength(24).IsRequired();
            e.Property(x => x.Value).HasPrecision(12, 2);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<OrderFile>(e =>
        {
            e.Property(x => x.Kind).HasMaxLength(16).IsRequired();
            e.Property(x => x.FileName).HasMaxLength(200).IsRequired();
            e.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
            e.Property(x => x.StoragePath).HasMaxLength(300).IsRequired();
            e.HasOne<Order>().WithMany(o => o.Files).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<AppSetting>(e =>
        {
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(64);
            e.Property(x => x.Value).HasMaxLength(4000).IsRequired();
        });

        b.Entity<DataProtectionKey>(e =>
        {
            e.Property(x => x.FriendlyName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Xml).IsRequired();
        });

        b.Entity<PortfolioWork>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(120).IsRequired();
            e.Property(x => x.Category).HasMaxLength(32).IsRequired();
            e.Property(x => x.Location).HasMaxLength(120);
            e.Property(x => x.Description).HasMaxLength(2000);
            e.HasIndex(x => new { x.IsPublished, x.SortOrder });
        });

        b.Entity<PortfolioPhoto>(e =>
        {
            e.Property(x => x.LargePath).HasMaxLength(300).IsRequired();
            e.Property(x => x.ThumbPath).HasMaxLength(300).IsRequired();
            e.Property(x => x.Caption).HasMaxLength(300);
            e.HasOne<PortfolioWork>().WithMany(w => w.Photos).HasForeignKey(x => x.WorkId).OnDelete(DeleteBehavior.Cascade);
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
            foreach (var fk in entity.GetForeignKeys())
                fk.SetConstraintName(ToSnake(fk.GetConstraintName()!));
            foreach (var index in entity.GetIndexes())
                index.SetDatabaseName(ToSnake(index.GetDatabaseName()!));
        }
    }

    private static string ToSnake(string name) =>
        Regex.Replace(name, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant();
}
