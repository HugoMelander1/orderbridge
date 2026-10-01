using OrderBridge.Worker;
using OrderBridge.Core;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddJsonConsole(o => o.IncludeScopes = true);
builder.Services.AddDbContext<BridgeDb>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Database")));
builder.Services.AddScoped<OutboxPublisher>();
builder.Services.AddHttpClient<OrderProcessor>(c => { c.BaseAddress = new Uri(builder.Configuration["InventoryUrl"] ?? "http://localhost:5081"); c.Timeout = TimeSpan.FromSeconds(8); });
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
