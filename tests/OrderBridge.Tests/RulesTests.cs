using OrderBridge.Core;
namespace OrderBridge.Tests;

public class RulesTests
{
    [Fact, Trait("Category", "Unit")]
    public void Replay_resets_processing_and_preserves_reservation_identity()
    {
        var order = new Order { Status = "Failed", Generation = 2, Attempts = 4, Error = "Timeout", NextAttemptAt = DateTime.UtcNow };
        var id = order.Id;
        order.QueueReplay();
        Assert.Equal(id, order.Id);
        Assert.Equal(3, order.Generation);
        Assert.Equal("Pending", order.Status);
        Assert.Equal(0, order.Attempts);
        Assert.Null(order.Error);
        Assert.Null(order.NextAttemptAt);
        Assert.Single(order.Events);
        Assert.Throws<InvalidOperationException>(() => order.QueueReplay());
    }
    [Fact, Trait("Category", "Unit")]
    public void Valid_order_is_accepted() => Assert.Null(Rules.Validate(new("Acme", [new("KB-01", 2)])));
    [Theory, Trait("Category", "Unit")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void Invalid_quantity_is_rejected(int quantity) => Assert.NotNull(Rules.Validate(new("Acme", [new("KB-01", quantity)])));
    [Fact, Trait("Category", "Unit")]
    public void Empty_customer_and_duplicate_products_are_rejected()
    {
        Assert.NotNull(Rules.Validate(new(" ", [new("KB-01", 1)])));
        Assert.NotNull(Rules.Validate(new("Acme", [new("KB-01", 1), new("KB-01", 2)])));
        Assert.NotNull(Rules.Validate(new("Acme", [])));
    }
}
