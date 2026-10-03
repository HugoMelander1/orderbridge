namespace OrderBridge.Core;

public record CreateOrder(string Customer, List<Line> Items);
