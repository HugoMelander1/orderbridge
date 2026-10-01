using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
namespace OrderBridge.Core;

public class OrderService(BridgeDb db, ILogger<OrderService>? log = null)
{
    public async Task<Order> Create(CreateOrder input)
    {
        if (Rules.Validate(input) is { } error) throw new ArgumentException(error);
        var skus = input.Items.Select(x => x.Sku).ToList();
        if (await db.Products.CountAsync(x => skus.Contains(x.Sku)) != skus.Count) throw new ArgumentException("Unknown product.");
        var order = new Order { Customer = input.Customer.Trim(), ItemsJson = JsonSerializer.Serialize(input.Items) };
        Rules.Event(order, "Order accepted; awaiting dispatch.");
        var message = Rules.Message(order);
        db.Orders.Add(order); db.Outbox.Add(message); await db.SaveChangesAsync();
        log?.LogInformation("Created order {OrderId}, message {MessageId}, correlation {CorrelationId}", order.Id, message.Id, order.CorrelationId);
        return order;
    }
    public async Task<Order?> Replay(Guid id)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var order = await db.Orders.FromSqlInterpolated($"SELECT * FROM \"Orders\" WHERE \"Id\"={id} FOR UPDATE").Include(x => x.Events).SingleOrDefaultAsync();
        if (order is null) return null;
        if (order.Status != "Failed") throw new InvalidOperationException("Only failed orders can be replayed.");
        order.Generation++; order.Attempts = 0; order.Status = "Pending"; order.Error = null; order.NextAttemptAt = null;
        var message = Rules.Message(order);
        Rules.Event(order, "Manual replay requested; reservation key remains unchanged."); db.Outbox.Add(message);
        await db.SaveChangesAsync(); await tx.CommitAsync();
        log?.LogInformation("Replayed order {OrderId}, message {MessageId}, correlation {CorrelationId}, generation {Generation}", order.Id, message.Id, order.CorrelationId, order.Generation);
        return order;
    }
}
