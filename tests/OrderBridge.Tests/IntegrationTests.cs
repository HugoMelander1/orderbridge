using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OrderBridge.Core;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace OrderBridge.Tests;

public class Infrastructure : IAsyncLifetime
{
    public PostgreSqlContainer Postgres { get; } = new PostgreSqlBuilder("postgres:17-alpine").Build();
    public RabbitMqContainer Rabbit { get; } = new RabbitMqBuilder("rabbitmq:4.3-management-alpine").Build();
    public BridgeDb Db() => new(new DbContextOptionsBuilder<BridgeDb>().UseNpgsql(Postgres.GetConnectionString()).Options);
    public async Task InitializeAsync() { await Task.WhenAll(Postgres.StartAsync(), Rabbit.StartAsync()); await using var db = Db(); await db.Database.MigrateAsync(); }
    public async Task DisposeAsync() { await Rabbit.DisposeAsync(); await Postgres.DisposeAsync(); }
}
[Trait("Category", "Integration")]
public class IntegrationTests(Infrastructure infra) : IClassFixture<Infrastructure>
{
    private async Task<Order> Create(int quantity = 1)
    {
        await using var db = infra.Db(); return await new OrderService(db).Create(new("Integration customer", [new("KB-01", quantity)]));
    }
    private async Task Process(Envelope envelope, HttpMessageHandler handler)
    {
        await using var db = infra.Db(); using var http = new HttpClient(handler, false) { BaseAddress = new Uri("http://inventory") };
        await new OrderProcessor(db, http, NullLogger<OrderProcessor>.Instance).Process(envelope, CancellationToken.None);
    }
    private static Envelope Message(Order order) => JsonSerializer.Deserialize<Envelope>(Rules.Message(order).Payload)!;
    [Fact]
    public async Task Reservation_is_durable_and_duplicate_requests_do_not_reduce_stock_twice()
    {
        var order = await Create(); var request = new ReservationRequest(order.Id, order.CorrelationId, [new("KB-01", 1)]);
        int before;
        await using (var db = infra.Db()) { before = (await db.Products.FindAsync("KB-01"))!.Stock; Assert.True((await new InventoryService(db).Reserve(request)).Reserved); }
        await using (var db = infra.Db()) { Assert.True((await new InventoryService(db).Reserve(request)).Reserved); Assert.Equal(before - 1, (await db.Products.FindAsync("KB-01"))!.Stock); Assert.Equal(1, await db.Reservations.CountAsync(x => x.OrderId == order.Id)); }
    }
    [Fact]
    public async Task Insufficient_stock_is_business_rejection_without_retry()
    {
        var order = await Create(); using var handler = new WarehouseHandler(infra) { UnavailableSku = "KB-01" };
        await Process(Message(order), handler);
        await using var db = infra.Db(); var saved = await db.Orders.FindAsync(order.Id); Assert.Equal("Rejected", saved!.Status); Assert.Equal(0, saved.Attempts); Assert.Null(saved.NextAttemptAt);
    }
    [Fact]
    public async Task Duplicate_messages_are_ignored_after_success()
    {
        var order = await Create(); using var handler = new WarehouseHandler(infra);
        var message = Message(order); await Process(message, handler); await Process(message, handler);
        await using var db = infra.Db(); Assert.Equal("Reserved", (await db.Orders.FindAsync(order.Id))!.Status); Assert.Equal(1, handler.Calls); Assert.Equal(1, await db.Reservations.CountAsync(x => x.OrderId == order.Id));
    }
    [Fact]
    public async Task Temporary_failure_schedules_retry_and_lost_response_does_not_duplicate_reservation()
    {
        var order = await Create(); using var handler = new WarehouseHandler(infra) { LoseResponse = true };
        await Process(Message(order), handler);
        await using (var db = infra.Db()) { order = (await db.Orders.FindAsync(order.Id))!; Assert.Equal("Pending", order.Status); Assert.Equal(1, order.Attempts); Assert.True(order.NextAttemptAt > DateTime.UtcNow); }
        handler.LoseResponse = false; await Process(Message(order), handler);
        await using (var db = infra.Db()) { Assert.Equal("Reserved", (await db.Orders.FindAsync(order.Id))!.Status); Assert.Equal(1, await db.Reservations.CountAsync(x => x.OrderId == order.Id)); }
    }
    [Fact]
    public async Task Exhausted_retries_publish_dead_letter_and_replay_uses_same_reservation_key()
    {
        var order = await Create(); using var handler = new WarehouseHandler(infra) { Fail = true };
        for (var i = 0; i < 4; i++) { await Process(Message(order), handler); await using var db = infra.Db(); order = (await db.Orders.FindAsync(order.Id))!; }
        Assert.Equal("Failed", order.Status); Assert.Equal(4, order.Attempts);
        await using (var db = infra.Db())
        {
            var row = await db.Outbox.SingleAsync(x => x.Queue == "orders.dead" && x.Payload.Contains(order.Id.ToString()));
            await using var connection = await Broker.Connect(infra.Rabbit.GetConnectionString()); await using var channel = await Broker.Channel(connection);
            await Broker.Publish(channel, row, CancellationToken.None);
            bool found = false;
            while (await channel.BasicGetAsync("orders.dead", true) is { } dead)
                if (JsonSerializer.Deserialize<Envelope>(dead.Body.Span)!.OrderId == order.Id) found = true;
            Assert.True(found);
            order = (await new OrderService(db).Replay(order.Id))!;
        }
        handler.Fail = false; await Process(Message(order), handler);
        await using (var db = infra.Db()) { Assert.Equal("Reserved", (await db.Orders.FindAsync(order.Id))!.Status); Assert.Equal(1, await db.Reservations.CountAsync(x => x.OrderId == order.Id)); }
    }
    [Fact]
    public async Task Replay_after_four_lost_responses_keeps_the_original_reservation()
    {
        var order = await Create();
        using var handler = new WarehouseHandler(infra) { LoseResponse = true };
        var staleMessage = Message(order);
        for (var attempt = 0; attempt < 4; attempt++)
        {
            await Process(Message(order), handler);
            await using var db = infra.Db();
            order = (await db.Orders.FindAsync(order.Id))!;
        }
        Assert.Equal("Failed", order.Status);
        await using (var db = infra.Db())
        {
            Assert.Equal(1, await db.Reservations.CountAsync(x => x.OrderId == order.Id && x.Reserved));
            order = (await new OrderService(db).Replay(order.Id))!;
        }
        var calls = handler.Calls;
        await Process(staleMessage, handler);
        Assert.Equal(calls, handler.Calls);
        handler.LoseResponse = false;
        await Process(Message(order), handler);
        await using (var db = infra.Db())
        {
            Assert.Equal("Reserved", (await db.Orders.FindAsync(order.Id))!.Status);
            Assert.Equal(1, await db.Reservations.CountAsync(x => x.OrderId == order.Id && x.Reserved));
        }
    }
    [Fact]
    public async Task Outbox_survives_context_restart_and_publishes_confirmed_persistent_message()
    {
        var order = await Create();
        await using var db = infra.Db(); Assert.True(await db.Outbox.AnyAsync(x => x.PublishedAt == null && x.Payload.Contains(order.Id.ToString())));
        await using var connection = await Broker.Connect(infra.Rabbit.GetConnectionString()); await using var channel = await Broker.Channel(connection);
        await new OutboxPublisher(db).Dispatch(channel, CancellationToken.None);
        Assert.True(await db.Outbox.AnyAsync(x => x.PublishedAt != null && x.Payload.Contains(order.Id.ToString())));
        bool found = false;
        while (await channel.BasicGetAsync("orders", true) is { } delivery)
        {
            if (JsonSerializer.Deserialize<Envelope>(delivery.Body.Span)!.OrderId == order.Id) { found = true; Assert.True(delivery.BasicProperties.Persistent); }
        }
        Assert.True(found);
    }
    [Fact]
    public async Task Concurrent_reservations_cannot_make_stock_negative()
    {
        await using (var db = infra.Db()) await db.Products.Where(x => x.Sku == "MS-02").ExecuteUpdateAsync(s => s.SetProperty(x => x.Stock, 1));
        async Task<ReservationResult> Reserve() { await using var db = infra.Db(); return await new InventoryService(db).Reserve(new(Guid.NewGuid(), Guid.NewGuid(), [new("MS-02", 1)])); }
        var results = await Task.WhenAll(Reserve(), Reserve()); Assert.Single(results, x => x.Reserved);
        await using (var db = infra.Db()) Assert.Equal(0, (await db.Products.FindAsync("MS-02"))!.Stock);
    }
    [Fact]
    public async Task Invalid_orders_and_changed_idempotency_payload_are_rejected()
    {
        await using var db = infra.Db(); var service = new OrderService(db);
        await Assert.ThrowsAsync<ArgumentException>(() => service.Create(new("Acme", [new("UNKNOWN", 1)])));
        await Assert.ThrowsAsync<ArgumentException>(() => service.Create(new("Acme", [new("KB-01", 0)])));
        var id = Guid.NewGuid(); await new InventoryService(db).Reserve(new(id, Guid.NewGuid(), [new("DK-03", 1)]));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new InventoryService(db).Reserve(new(id, Guid.NewGuid(), [new("DK-03", 2)])));
    }
    private class WarehouseHandler(Infrastructure infra) : HttpMessageHandler
    {
        public bool Fail { get; set; }
        public bool LoseResponse { get; set; }
        public string? UnavailableSku { get; set; }
        public int Calls { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            if (Fail) return new(HttpStatusCode.ServiceUnavailable);
            var input = (await request.Content!.ReadFromJsonAsync<ReservationRequest>(ct))!;
            await using var db = infra.Db(); var result = await new InventoryService(db).Reserve(input, UnavailableSku);
            if (LoseResponse) throw new HttpRequestException("Response lost after database commit.");
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(result) };
        }
    }
}
