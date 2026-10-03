using Microsoft.EntityFrameworkCore;
using OrderBridge.Core;

namespace OrderBridge.Inventory;

public static class InventoryEndpoints
{
    public static WebApplication MapInventoryEndpoints(this WebApplication app)
    {
        app.MapPost("/reservations", async (ReservationRequest input, BridgeDb db, InventoryService inventory, ILogger<InventoryService> log, HttpContext context) =>
        {
            if (input.OrderId == Guid.Empty || Rules.ValidateLines(input.Items) is not null) return Results.Problem("Invalid reservation.", statusCode: 400);
            var settings = await db.Demo.AsNoTracking().SingleAsync();
            log.LogInformation("Reservation {OrderId} correlation {CorrelationId} message {MessageId}", input.OrderId, input.CorrelationId, context.Request.Headers["X-Message-ID"].ToString());
            if (app.Environment.IsDevelopment() && settings.Mode == "TemporaryError") return Results.Problem("Simulated temporary warehouse failure.", statusCode: 503);
            if (app.Environment.IsDevelopment() && settings.Mode == "Delayed") await Task.Delay(TimeSpan.FromSeconds(12));
            try { return Results.Ok(await inventory.Reserve(input, app.Environment.IsDevelopment() && settings.Mode == "Insufficient" ? settings.Sku : null)); }
            catch (InvalidOperationException ex) { return Results.Problem(ex.Message, statusCode: 409); }
        });
        if (app.Environment.IsDevelopment())
        {
            app.MapGet("/demo", async (BridgeDb db) => await db.Demo.AsNoTracking().SingleAsync());
            app.MapPost("/demo", async (DemoSettings input, BridgeDb db) =>
            {
                if (!new[] { "Normal", "Delayed", "TemporaryError", "Insufficient" }.Contains(input.Mode) || (input.Mode == "Insufficient" && !await db.Products.AnyAsync(x => x.Sku == input.Sku))) return Results.Problem("Select a supported mode and product.", statusCode: 400);
                var settings = await db.Demo.SingleAsync(); settings.Mode = input.Mode; settings.Sku = input.Sku; await db.SaveChangesAsync(); return Results.Ok(settings);
            });
        }
        return app;
    }
}
