using OrderBridge.Core;
namespace OrderBridge.Tests;

public class RulesTests
{
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
