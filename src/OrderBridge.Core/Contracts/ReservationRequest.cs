namespace OrderBridge.Core;

public record ReservationRequest(Guid OrderId, Guid CorrelationId, List<Line> Items);
