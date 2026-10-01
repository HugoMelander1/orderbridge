using Microsoft.EntityFrameworkCore;
using OrderBridge.Core;

if (args.Contains("--health-probe")) { Environment.Exit(await HealthProbe.Run()); return; }
var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddJsonConsole(o => o.IncludeScopes = true);
builder.Services.AddDbContext<BridgeDb>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Database")));
builder.Services.AddHttpClient("inventory", c => { c.BaseAddress = new Uri(builder.Configuration["InventoryUrl"] ?? "http://localhost:5081"); c.Timeout = TimeSpan.FromSeconds(8); });
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(builder.Configuration["FrontendOrigin"] ?? "http://localhost:5173").AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddProblemDetails();
builder.Services.AddScoped<OrderService>();
builder.Services.AddOpenApi();
var app = builder.Build();
app.UseExceptionHandler(); app.UseCors();
using (var scope = app.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<BridgeDb>().Database.MigrateAsync();
app.MapOpenApi();
app.MapGet("/health", async (BridgeDb db) => await db.Database.CanConnectAsync() ? Results.Ok(new { status = "Healthy" }) : Results.StatusCode(503));
app.MapGet("/api/dependencies", async (BridgeDb db, IHttpClientFactory http, IConfiguration config) =>
{
    bool postgres = false, rabbit = false, inventory = false;
    try { postgres = await db.Database.CanConnectAsync(); } catch { }
    try { using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3)); await using var connection = await Broker.Connect(config["RabbitUri"]!, timeout.Token); rabbit = connection.IsOpen; } catch { }
    try { inventory = (await http.CreateClient("inventory").GetAsync("/health")).IsSuccessStatusCode; } catch { }
    return new { postgres, rabbit, inventory, demoEnabled = app.Environment.IsDevelopment() };
});
app.MapGet("/api/products", async (BridgeDb db) => await db.Products.OrderBy(x => x.Sku).ToListAsync());
app.MapGet("/api/dashboard", async (BridgeDb db) => await db.Orders.GroupBy(x => x.Status).Select(g => new { status = g.Key, count = g.Count() }).ToListAsync());
app.MapGet("/api/orders", async (BridgeDb db, string? search, string? status, int page = 1) =>
{
    var query = db.Orders.AsNoTracking();
    if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Customer.Contains(search) || x.Id.ToString().Contains(search));
    if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
    var total = await query.CountAsync();
    var items = await query.OrderByDescending(x => x.CreatedAt).Skip((Math.Clamp(page, 1, 100000) - 1) * 10).Take(10).ToListAsync();
    return Results.Ok(new { total, items });
});
app.MapGet("/api/orders/{id:guid}", async (Guid id, BridgeDb db) => await db.Orders.AsNoTracking().Include(x => x.Events).SingleOrDefaultAsync(x => x.Id == id) is { } order ? Results.Ok(order) : Results.Problem("Order not found.", statusCode: 404));
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
    app.MapGet("/api/reservations/{id:guid}", async (Guid id, BridgeDb db) => new { count = await db.Reservations.CountAsync(x => x.OrderId == id && x.Reserved) });
}
app.Run();
