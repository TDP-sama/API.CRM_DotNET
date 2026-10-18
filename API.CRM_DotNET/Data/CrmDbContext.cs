using API.CRM_DotNET.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace API.CRM_DotNET.Data;

public class CrmDbContext(DbContextOptions<CrmDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<Activity> Activities => Set<Activity>();

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<StockLevel> StockLevels => Set<StockLevel>();
    public DbSet<StockTransaction> StockTransactions => Set<StockTransaction>();
    public DbSet<StockCount> StockCounts => Set<StockCount>();
    public DbSet<StockCountLine> StockCountLines => Set<StockCountLine>();

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Opportunity> Opportunities => Set<Opportunity>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
        configurationBuilder.Properties<string>().HaveMaxLength(500);
        // All times are stored as UTC; mark them as such when read so JSON carries the "Z" suffix
        // and browsers convert to local time correctly.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcDateTimeConverter>();
    }

    private class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        v => v.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(v, DateTimeKind.Utc) : v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Everything is Restrict by default (avoids SQL Server "multiple cascade paths");
        // owned children opt back into Cascade below.
        foreach (var fk in b.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;

        b.Entity<User>(e =>
        {
            e.HasIndex(x => x.Username).IsUnique();
            e.Property(x => x.Username).HasMaxLength(100);
        });
        b.Entity<Role>(e =>
        {
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.Name).HasMaxLength(100);
        });
        b.Entity<UserRole>(e =>
        {
            e.HasKey(x => new { x.UserId, x.RoleId });
            e.HasOne(x => x.User).WithMany(x => x.UserRoles).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Role).WithMany(x => x.UserRoles).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<RolePermission>(e =>
        {
            e.HasKey(x => new { x.RoleId, x.Permission });
            e.Property(x => x.Permission).HasMaxLength(100);
            e.HasOne(x => x.Role).WithMany(x => x.Permissions).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Customer>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(50);
            e.Property(x => x.Notes).HasMaxLength(4000);
        });
        b.Entity<Contact>().HasOne(x => x.Customer).WithMany(x => x.Contacts).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Activity>(e =>
        {
            e.HasOne(x => x.Customer).WithMany(x => x.Activities).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Content).HasMaxLength(4000);
        });

        b.Entity<Product>(e =>
        {
            e.HasIndex(x => x.Sku).IsUnique();
            e.Property(x => x.Sku).HasMaxLength(50);
        });
        b.Entity<Warehouse>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(50);
        });
        b.Entity<StockLevel>().HasKey(x => new { x.ProductId, x.WarehouseId });
        b.Entity<StockTransaction>().HasIndex(x => x.CreatedAt);
        b.Entity<StockCount>().Property(x => x.Code).HasMaxLength(50);
        b.Entity<StockCountLine>().HasOne(x => x.StockCount).WithMany(x => x.Lines).OnDelete(DeleteBehavior.Cascade);

        b.Entity<Order>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(50);
            e.HasIndex(x => x.OrderDate);
        });
        b.Entity<OrderItem>().HasOne(x => x.Order).WithMany(x => x.Items).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Opportunity>().Property(x => x.Note).HasMaxLength(4000);
    }
}
