using RabbitMQ.Client;
namespace OrderBridge.Core;

public static class Broker
{
    public static async Task<IConnection> Connect(string uri, CancellationToken ct = default) => await new ConnectionFactory { Uri = new Uri(uri), AutomaticRecoveryEnabled = true }.CreateConnectionAsync(ct);
    public static async Task<IChannel> Channel(IConnection connection, CancellationToken ct = default)
    {
        var channel = await connection.CreateChannelAsync(new CreateChannelOptions(true, true), ct);
        await channel.QueueDeclareAsync("orders.dead", true, false, false, cancellationToken: ct);
        await channel.QueueDeclareAsync("orders", true, false, false, new Dictionary<string, object?> { ["x-dead-letter-exchange"] = "", ["x-dead-letter-routing-key"] = "orders.dead" }, cancellationToken: ct);
        return channel;
    }
    public static async Task Publish(IChannel channel, Outbox row, CancellationToken ct) => await channel.BasicPublishAsync("", row.Queue, true, new BasicProperties { Persistent = true, MessageId = row.Id.ToString(), ContentType = "application/json" }, System.Text.Encoding.UTF8.GetBytes(row.Payload), ct);
}
