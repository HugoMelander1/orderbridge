namespace OrderBridge.Core;

public class Outbox
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Payload { get; set; } = "";
    public string Queue { get; set; } = "orders";
    public DateTime AvailableAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }
}
