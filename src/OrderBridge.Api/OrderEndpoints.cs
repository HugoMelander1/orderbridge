using Microsoft.EntityFrameworkCore;
using OrderBridge.Core;

namespace OrderBridge.Api;

public static class OrderEndpoints
{
    public static WebApplication MapOrderEndpoints(this WebApplication app)
    {
        app.MapGet("/api/dependencies", async (BridgeDb db, IHttpClientFactory http, IConfiguration config) =>
        {
            bool postgres = false, rabbit = false, inventory = false;
            try { postgres = await db.Database.CanConnectAsync(); } catch { }
            try { using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3)); await using var connection = await Broker.Connect(config["RabbitUri"]!, timeout.Token); rabbit = connection.IsOpen; } catch { }
            try { inventory = (await http.CreateClient("inventory").GetAsync("/health")).IsSuccessStatusCode; } catch { }
            return new { postgres, rabbit, inventory, demoEnabled = app.Environment.IsDevelopment() };
        });
        app.MapGet("/api/products", (OrderQueries queries) => queries.Products());
        app.MapGet("/api/dashboard", (OrderQueries queries) => queries.StatusCounts());
        app.MapGet("/api/orders", (OrderQueries queries, string? search, string? status, int page = 1) => queries.Search(search, status, page));
        app.MapGet("/api/orders/{id:guid}", async (Guid id, OrderQueries queries) => await queries.Find(id) is { } order ? Results.Ok(order) : Results.Problem("Order not found.", statusCode: 404));
        app.MapPost("/api/orders", async (CreateOrder input, OrderService service) =>
        {
            try { var order = await service.Create(input); return Results.Created($"/api/orders/{order.Id}", order); }
            catch (ArgumentException ex) { return Results.Problem(ex.Message, statusCode: 400); }
        });
        if (app.Environment.IsDevelopment())
        {
            app.MapPost("/api/orders/{id:guid}/replay", async (Guid id, OrderService service) =>
            {
                try { var order = await service.Replay(id); return order is null ? Results.Problem("Order not found.", statusCode: 404) : Results.Ok(order); }
                catch (InvalidOperationException ex) { return Results.Problem(ex.Message, statusCode: 409); }
            });
            app.MapGet("/api/demo", async (IHttpClientFactory http) => Results.Content(await http.CreateClient("inventory").GetStringAsync("/demo"), "application/json"));
            app.MapPost("/api/demo", async (DemoSettings settings, IHttpClientFactory http) =>
            {
                var response = await http.CreateClient("inventory").PostAsJsonAsync("/demo", settings);
                return Results.Content(await response.Content.ReadAsStringAsync(), "application/json", statusCode: (int)response.StatusCode);
            });
            app.MapGet("/api/reservations/{id:guid}", async (Guid id, OrderQueries queries) => new { count = await queries.ReservationCount(id) });
        }
        return app;
    }
}
