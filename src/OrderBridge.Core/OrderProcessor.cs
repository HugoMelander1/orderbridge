using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
namespace OrderBridge.Core;

public class OrderProcessor(BridgeDb db, HttpClient inventory, ILogger<OrderProcessor> log)
{
    public async Task Process(Envelope message, CancellationToken ct)
    {
        using var scope = log.BeginScope(new Dictionary<string, object> { ["OrderId"] = message.OrderId, ["MessageId"] = message.MessageId, ["CorrelationId"] = message.CorrelationId });
        await db.Orders.Where(x => x.Id == message.OrderId && x.Generation == message.Generation && x.Attempts == message.Attempt && (x.Status == "Pending" || x.Status == "Processing"))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "Processing"), ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var order = await db.Orders.FromSqlInterpolated($"SELECT * FROM \"Orders\" WHERE \"Id\"={message.OrderId} FOR UPDATE").Include(x => x.Events).SingleOrDefaultAsync(ct);
        if (order is null || order.Generation != message.Generation || order.Attempts != message.Attempt || order.Status is "Reserved" or "Rejected" or "Failed") return;
        order.Status = "Processing";
        Rules.Event(order, $"Processing attempt {order.Attempts + 1}; message {message.MessageId}.");
        string? failure = null; bool transient = true;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/reservations") { Content = JsonContent.Create(new ReservationRequest(order.Id, order.CorrelationId, JsonSerializer.Deserialize<List<Line>>(order.ItemsJson)!)) };
            request.Headers.Add("X-Correlation-ID", order.CorrelationId.ToString()); request.Headers.Add("X-Message-ID", message.MessageId.ToString());
            using var response = await inventory.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ReservationResult>(ct) ?? throw new HttpRequestException("Empty reservation response.");
                order.Status = result.Reserved ? "Reserved" : "Rejected"; order.Error = null; order.NextAttemptAt = null; Rules.Event(order, result.Detail);
            }
            else { transient = (int)response.StatusCode >= 500 || response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests; failure = $"Inventory returned HTTP {(int)response.StatusCode}."; }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException && !ct.IsCancellationRequested) { failure = "Inventory unavailable, response invalid, or request timed out."; }
        if (failure is not null)
        {
            order.Attempts++; order.Error = failure;
            if (transient && order.Attempts <= Rules.RetrySeconds.Length)
            {
                order.Status = "Pending"; order.NextAttemptAt = DateTime.UtcNow.AddSeconds(Rules.RetrySeconds[order.Attempts - 1]);
                Rules.Event(order, $"{failure} Retry {order.Attempts} scheduled at {order.NextAttemptAt:O}."); db.Outbox.Add(Rules.Message(order, order.NextAttemptAt));
            }
            else
            {
                order.Status = "Failed"; order.NextAttemptAt = null; Rules.Event(order, $"{failure} Processing stopped; message routed to orders.dead."); db.Outbox.Add(Rules.Message(order, queue: "orders.dead"));
            }
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        log.LogInformation("Order completed attempt with {Status}", order.Status);
    }
}
