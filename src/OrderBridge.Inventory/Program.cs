using OrderBridge.Inventory;
using Microsoft.EntityFrameworkCore;
using OrderBridge.Core;
if (args.Contains("--health-probe")) { Environment.Exit(await HealthProbe.Run()); return; }
var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddJsonConsole(o => o.IncludeScopes = true);
builder.Services.AddDbContext<BridgeDb>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Database")));
builder.Services.AddScoped<InventoryService>();
builder.Services.AddProblemDetails(); builder.Services.AddOpenApi();
var app = builder.Build();
app.UseExceptionHandler(); app.MapOpenApi();
app.MapGet("/health", async (BridgeDb db) => await db.Database.CanConnectAsync() ? Results.Ok() : Results.StatusCode(503));
app.MapInventoryEndpoints();
app.Run();
