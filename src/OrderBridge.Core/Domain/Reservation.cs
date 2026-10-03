namespace OrderBridge.Core;

public class Reservation
{
    public Guid OrderId { get; set; }
    public string RequestJson { get; set; } = "";
    public bool Reserved { get; set; }
    public string Detail { get; set; } = "";
}
