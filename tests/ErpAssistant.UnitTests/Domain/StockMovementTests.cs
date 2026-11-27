using ErpAssistant.Domain.Common;
using ErpAssistant.Domain.Inventory;
using FluentAssertions;

namespace ErpAssistant.UnitTests.Domain;

public class StockMovementTests
{
    [Theory]
    [InlineData(StockMovementType.Sale, 5, -5)]
    [InlineData(StockMovementType.TransferOut, 5, -5)]
    [InlineData(StockMovementType.Purchase, -5, 5)]
    [InlineData(StockMovementType.Return, 5, 5)]
    [InlineData(StockMovementType.Adjustment, -3, -3)]
    public void Create_normalizes_sign_by_type(StockMovementType type, int input, int expected)
    {
        StockMovement.Create(1, 1, type, input, DateTime.Today).Quantity.Should().Be(expected);
    }

    [Fact]
    public void Zero_quantity_is_rejected()
    {
        var act = () => StockMovement.Create(1, 1, StockMovementType.Adjustment, 0, DateTime.Today);
        act.Should().Throw<DomainException>();
    }
}
