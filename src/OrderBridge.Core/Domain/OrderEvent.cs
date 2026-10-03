namespace OrderBridge.Core;

public class OrderEvent
{
    public long Id { get; set; }
    public Guid OrderId { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
    public string Detail { get; set; } = "";
}
