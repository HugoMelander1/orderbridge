namespace OrderBridge.Core;

public record Envelope(Guid MessageId, Guid OrderId, Guid CorrelationId, int Generation, int Attempt);
