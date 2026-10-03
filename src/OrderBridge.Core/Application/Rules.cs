namespace OrderBridge.Core;

public static class Rules
{
    public static string? Validate(CreateOrder input) => string.IsNullOrWhiteSpace(input.Customer) || input.Customer.Length > 100 ? "Customer must contain 1–100 characters." : ValidateLines(input.Items);
    public static string? ValidateLines(List<Line>? items) => items is null || items.Count is < 1 or > 10 || items.Any(x => x is null || string.IsNullOrWhiteSpace(x.Sku) || x.Quantity is < 1 or > 100) || items.Select(x => x.Sku).Distinct().Count() != items.Count ? "Provide 1–10 distinct products with quantities between 1 and 100." : null;
    public static readonly int[] RetrySeconds = [5, 15, 30];
    public static void Event(Order order, string detail) => order.AddEvent(detail);
    public static Outbox Message(Order order, DateTime? due = null, string queue = "orders")
    {
        var messageId = Guid.NewGuid();
        return new() { Id = messageId, Queue = queue, AvailableAt = due ?? DateTime.UtcNow, Payload = System.Text.Json.JsonSerializer.Serialize(new Envelope(messageId, order.Id, order.CorrelationId, order.Generation, order.Attempts)) };
    }
}
