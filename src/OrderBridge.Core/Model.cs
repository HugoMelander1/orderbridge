using Microsoft.EntityFrameworkCore;

namespace OrderBridge.Core;

public record Line(string Sku, int Quantity);
public record CreateOrder(string Customer, List<Line> Items);
public record Envelope(Guid MessageId, Guid OrderId, Guid CorrelationId, int Generation, int Attempt);
public record ReservationRequest(Guid OrderId, Guid CorrelationId, List<Line> Items);
public record ReservationResult(bool Reserved, string Detail);
public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CorrelationId { get; set; } = Guid.NewGuid();
    public string Customer { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public string ItemsJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int Attempts { get; set; }
    public int Generation { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public string? Error { get; set; }
    public List<OrderEvent> Events { get; set; } = [];
}
public class OrderEvent
{
    public long Id { get; set; }
    public Guid OrderId { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
    public string Detail { get; set; } = "";
}
public class Outbox
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Payload { get; set; } = "";
    public string Queue { get; set; } = "orders";
    public DateTime AvailableAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }
}
public class Product
{
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public int Stock { get; set; }
}
public class Reservation
{
    public Guid OrderId { get; set; }
    public string RequestJson { get; set; } = "";
    public bool Reserved { get; set; }
    public string Detail { get; set; } = "";
}
public class DemoSettings
{
    public int Id { get; set; } = 1;
    public string Mode { get; set; } = "Normal";
    public string? Sku { get; set; }
}
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
public static class Rules
{
    public static string? Validate(CreateOrder input) => string.IsNullOrWhiteSpace(input.Customer) || input.Customer.Length > 100 ? "Customer must contain 1–100 characters." : ValidateLines(input.Items);
    public static string? ValidateLines(List<Line>? items) => items is null || items.Count is < 1 or > 10 || items.Any(x => x is null || string.IsNullOrWhiteSpace(x.Sku) || x.Quantity is < 1 or > 100) || items.Select(x => x.Sku).Distinct().Count() != items.Count ? "Provide 1–10 distinct products with quantities between 1 and 100." : null;
    public static readonly int[] RetrySeconds = [5, 15, 30];
    public static void Event(Order order, string detail) => order.Events.Add(new OrderEvent { OrderId = order.Id, Detail = detail });
    public static Outbox Message(Order order, DateTime? due = null, string queue = "orders")
    {
        var messageId = Guid.NewGuid();
        return new() { Id = messageId, Queue = queue, AvailableAt = due ?? DateTime.UtcNow, Payload = System.Text.Json.JsonSerializer.Serialize(new Envelope(messageId, order.Id, order.CorrelationId, order.Generation, order.Attempts)) };
    }
}
