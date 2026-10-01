using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
namespace OrderBridge.Core;

public class OutboxPublisher(BridgeDb db)
{
    public async Task<int> Dispatch(IChannel channel, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var rows = await db.Outbox.FromSqlRaw("SELECT * FROM \"Outbox\" WHERE \"PublishedAt\" IS NULL AND \"AvailableAt\" <= now() ORDER BY \"AvailableAt\" LIMIT 20 FOR UPDATE SKIP LOCKED").ToListAsync(ct);
        foreach (var row in rows) { await Broker.Publish(channel, row, ct); row.PublishedAt = DateTime.UtcNow; }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return rows.Count;
    }
}
