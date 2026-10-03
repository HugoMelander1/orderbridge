using OrderBridge.Api;
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
builder.Services.AddScoped<OrderQueries>();
builder.Services.AddOpenApi();
var app = builder.Build();
app.UseExceptionHandler(); app.UseCors();
using (var scope = app.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<BridgeDb>().Database.MigrateAsync();
app.MapOpenApi();
app.MapGet("/health", async (BridgeDb db) => await db.Database.CanConnectAsync() ? Results.Ok(new { status = "Healthy" }) : Results.StatusCode(503));
app.MapOrderEndpoints();
app.Run();
