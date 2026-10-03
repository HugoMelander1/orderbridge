using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using RabbitMQ.Client;
namespace OrderBridge.Core;

public class OutboxPublisher(BridgeDb db, ILogger<OutboxPublisher>? log = null)
{
    public async Task<int> Dispatch(IChannel channel, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var rows = await db.Outbox.FromSqlRaw("SELECT * FROM \"Outbox\" WHERE \"PublishedAt\" IS NULL AND \"AvailableAt\" <= now() ORDER BY \"AvailableAt\" LIMIT 20 FOR UPDATE SKIP LOCKED").ToListAsync(ct);
        foreach (var row in rows)
        {
            await Broker.Publish(channel, row, ct);
            row.PublishedAt = DateTime.UtcNow;
            var message = JsonSerializer.Deserialize<Envelope>(row.Payload)!;
            log?.LogInformation("Confirmed publication of {MessageId}, order {OrderId}, correlation {CorrelationId} to {Queue}", message.MessageId, message.OrderId, message.CorrelationId, row.Queue);
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return rows.Count;
    }
}
