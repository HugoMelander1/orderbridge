using System.Text.Json;
using Microsoft.EntityFrameworkCore;
namespace OrderBridge.Core;

public class InventoryService(BridgeDb db)
{
    public async Task<ReservationResult> Reserve(ReservationRequest request, string? unavailableSku = null)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({request.OrderId.ToString()},0))");
        var canonical = JsonSerializer.Serialize(request.Items.OrderBy(x => x.Sku).ToList());
        var existing = await db.Reservations.FindAsync(request.OrderId);
        if (existing is not null)
        {
            if (existing.RequestJson != canonical) throw new InvalidOperationException("Reservation key reused with different items.");
            return new(existing.Reserved, existing.Detail);
        }
        var products = new List<Product>();
        foreach (var line in request.Items.OrderBy(x => x.Sku))
        {
            var product = await db.Products.FromSqlInterpolated($"SELECT * FROM \"Products\" WHERE \"Sku\"={line.Sku} FOR UPDATE").SingleOrDefaultAsync();
            if (product is not null) products.Add(product);
        }
        var reserved = products.Count == request.Items.Count && request.Items.All(line => line.Sku != unavailableSku && products.Single(p => p.Sku == line.Sku).Stock >= line.Quantity);
        var detail = reserved ? "Inventory reserved." : "Insufficient inventory; no products were reserved.";
        if (reserved) foreach (var line in request.Items) products.Single(x => x.Sku == line.Sku).Stock -= line.Quantity;
        db.Reservations.Add(new Reservation { OrderId = request.OrderId, RequestJson = canonical, Reserved = reserved, Detail = detail });
        await db.SaveChangesAsync(); await tx.CommitAsync(); return new(reserved, detail);
    }
}
