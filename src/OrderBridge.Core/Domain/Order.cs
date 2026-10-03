namespace OrderBridge.Core;

public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CorrelationId { get; set; } = Guid.NewGuid();
    public string Customer { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public string ItemsJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int Attempts { get; set; }
    public int Generation { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public string? Error { get; set; }
    public List<OrderEvent> Events { get; set; } = [];

    public void AddEvent(string detail)
    {
        Events.Add(new OrderEvent { OrderId = Id, Detail = detail });
    }

    public void QueueReplay()
    {
        if (Status != "Failed")
        {
            throw new InvalidOperationException("Only failed orders can be replayed.");
        }

        Generation++;
        Attempts = 0;
        Status = "Pending";
        Error = null;
        NextAttemptAt = null;
        AddEvent("Manual replay requested; reservation key remains unchanged.");
    }
}
