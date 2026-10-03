using Microsoft.EntityFrameworkCore;

namespace OrderBridge.Core;

public class BridgeDb(DbContextOptions<BridgeDb> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderEvent> Events => Set<OrderEvent>();
    public DbSet<Outbox> Outbox => Set<Outbox>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<DemoSettings> Demo => Set<DemoSettings>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Order>().HasMany(x => x.Events).WithOne().HasForeignKey(x => x.OrderId);
        b.Entity<Outbox>().HasIndex(x => new { x.PublishedAt, x.AvailableAt });
        b.Entity<Product>().HasKey(x => x.Sku);
        b.Entity<Product>().ToTable(t => t.HasCheckConstraint("stock_nonnegative", "\"Stock\" >= 0"));
        b.Entity<Reservation>().HasKey(x => x.OrderId);
        b.Entity<Product>().HasData(new Product { Sku = "KB-01", Name = "Mechanical keyboard", Stock = 100 }, new Product { Sku = "MS-02", Name = "Wireless mouse", Stock = 100 }, new Product { Sku = "DK-03", Name = "USB-C dock", Stock = 100 });
        b.Entity<DemoSettings>().HasData(new DemoSettings());
    }
}
