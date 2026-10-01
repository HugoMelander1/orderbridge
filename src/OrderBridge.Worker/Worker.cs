using System.Text.Json;
using OrderBridge.Core;
namespace OrderBridge.Worker;

public class Worker(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = await Broker.Connect(configuration["RabbitUri"]!, stoppingToken);
                await using var publisher = await Broker.Channel(connection, stoppingToken);
                await using var consumer = await Broker.Channel(connection, stoppingToken);
                while (!stoppingToken.IsCancellationRequested)
                {
                    using (var scope = scopes.CreateScope()) await scope.ServiceProvider.GetRequiredService<OutboxPublisher>().Dispatch(publisher, stoppingToken);
                    var delivery = await consumer.BasicGetAsync("orders", false, stoppingToken);
                    if (delivery is null) { await Task.Delay(500, stoppingToken); continue; }
                    Envelope? message;
                    try { message = JsonSerializer.Deserialize<Envelope>(delivery.Body.Span); }
                    catch (JsonException) { await consumer.BasicNackAsync(delivery.DeliveryTag, false, false, stoppingToken); continue; }
                    if (message is null || message.OrderId == Guid.Empty || message.MessageId == Guid.Empty || message.Attempt < 0 || message.Generation < 0) { await consumer.BasicNackAsync(delivery.DeliveryTag, false, false, stoppingToken); continue; }
                    using (var scope = scopes.CreateScope()) await scope.ServiceProvider.GetRequiredService<OrderProcessor>().Process(message, stoppingToken);
                    await consumer.BasicAckAsync(delivery.DeliveryTag, false, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Worker connection or processing failed; reconnecting in five seconds"); await Task.Delay(5000, stoppingToken); }
        }
    }
}
