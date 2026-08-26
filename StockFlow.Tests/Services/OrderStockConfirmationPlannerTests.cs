using StockFlow.Entities;
using StockFlow.Services.Orders;

namespace StockFlow.Tests.Services;

public sealed class OrderStockConfirmationPlannerTests
{
    private readonly OrderStockConfirmationPlanner planner = new();

    [Fact]
    public void CreatePlan_ForSaleWithSufficientStock_ReturnsStockOutPlan()
    {
        var items = new[]
        {
            new OrderStockConfirmationItem(11, 3, 10),
            new OrderStockConfirmationItem(22, 2, 5)
        };

        var decision = planner.CreatePlan(OrderType.Sale, items);

        Assert.True(decision.IsApproved);
        Assert.Null(decision.Failure);
        var plan = Assert.IsType<OrderStockConfirmationPlan>(decision.Plan);
        Assert.Equal(StockMovementType.StockOut, plan.MovementType);
        Assert.Equal(7, plan.NewStockQuantities[11]);
        Assert.Equal(3, plan.NewStockQuantities[22]);
    }

    [Fact]
    public void CreatePlan_ForSaleWithInsufficientStock_ReturnsFailureContext()
    {
        var items = new[]
        {
            new OrderStockConfirmationItem(11, 3, 10),
            new OrderStockConfirmationItem(22, 6, 5)
        };

        var decision = planner.CreatePlan(OrderType.Sale, items);

        Assert.False(decision.IsApproved);
        Assert.Null(decision.Plan);
        var failure = Assert.IsType<OrderStockConfirmationFailure>(decision.Failure);
        Assert.Equal(OrderServiceErrorCodes.InsufficientStock, failure.ErrorCode);
        Assert.Equal(22, failure.ProductId);
        Assert.Equal(6, failure.RequestedQuantity);
        Assert.Equal(5, failure.AvailableQuantity);
    }

    [Fact]
    public void CreatePlan_ForPurchaseWithinRange_ReturnsStockInPlan()
    {
        var items = new[]
        {
            new OrderStockConfirmationItem(11, 3, 10),
            new OrderStockConfirmationItem(22, 2, 5)
        };

        var decision = planner.CreatePlan(OrderType.Purchase, items);

        Assert.True(decision.IsApproved);
        Assert.Null(decision.Failure);
        var plan = Assert.IsType<OrderStockConfirmationPlan>(decision.Plan);
        Assert.Equal(StockMovementType.StockIn, plan.MovementType);
        Assert.Equal(13, plan.NewStockQuantities[11]);
        Assert.Equal(7, plan.NewStockQuantities[22]);
    }

    [Fact]
    public void CreatePlan_ForPurchaseOverflow_ReturnsFailureContext()
    {
        var items = new[]
        {
            new OrderStockConfirmationItem(11, 3, 10),
            new OrderStockConfirmationItem(22, 1, int.MaxValue)
        };

        var decision = planner.CreatePlan(OrderType.Purchase, items);

        Assert.False(decision.IsApproved);
        Assert.Null(decision.Plan);
        var failure = Assert.IsType<OrderStockConfirmationFailure>(decision.Failure);
        Assert.Equal(OrderServiceErrorCodes.StockQuantityOutOfRange, failure.ErrorCode);
        Assert.Equal(22, failure.ProductId);
        Assert.Equal(1, failure.RequestedQuantity);
        Assert.Equal(int.MaxValue, failure.AvailableQuantity);
    }
}
