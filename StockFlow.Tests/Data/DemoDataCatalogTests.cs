using StockFlow.Data;
using StockFlow.Entities;

namespace StockFlow.Tests.Data;

public sealed class DemoDataCatalogTests
{
    [Fact]
    public void Create_ReturnsDeterministicInternallyConsistentGraph()
    {
        var dataSet = DemoDataCatalog.Create("admin-user", "employee-user");

        Assert.Equal(DemoDataCatalog.ExpectedCategoryCount, dataSet.Categories.Count);
        Assert.Equal(DemoDataCatalog.ExpectedProductCount, dataSet.Products.Count);
        Assert.Equal(DemoDataCatalog.ExpectedCustomerCount, dataSet.Customers.Count);
        Assert.Equal(DemoDataCatalog.ExpectedSupplierCount, dataSet.Suppliers.Count);
        Assert.Equal(DemoDataCatalog.ExpectedOrderCount, dataSet.Orders.Count);
        Assert.Equal(
            DemoDataCatalog.ExpectedOrderItemCount,
            dataSet.Orders.Sum(order => order.Items.Count));
        Assert.Equal(
            DemoDataCatalog.ExpectedStockMovementCount,
            dataSet.Orders.Sum(order => order.StockMovements.Count));

        Assert.Equal(14, dataSet.Orders.Count(order => order.Type == OrderType.Sale));
        Assert.Equal(10, dataSet.Orders.Count(order => order.Type == OrderType.Purchase));
        Assert.Equal(12, dataSet.Orders.Count(order => order.Status == OrderStatus.Confirmed));
        Assert.Equal(7, dataSet.Orders.Count(order => order.Status == OrderStatus.Draft));
        Assert.Equal(5, dataSet.Orders.Count(order => order.Status == OrderStatus.Cancelled));
        Assert.Equal(
            DemoDataCatalog.ExpectedLowStockProductCount,
            dataSet.Products.Count(product =>
                product.StockQuantity <= product.MinimumStockQuantity));

        Assert.All(dataSet.Orders, order =>
        {
            Assert.Contains(order.CreatedByUserId, new[] { "admin-user", "employee-user" });
            Assert.Equal(
                order.Items.Sum(item => item.Quantity * item.UnitPrice),
                order.TotalAmount);

            if (order.Status == OrderStatus.Confirmed)
            {
                Assert.Equal(order.Items.Count, order.StockMovements.Count);
                var expectedMovementType = order.Type == OrderType.Purchase
                    ? StockMovementType.StockIn
                    : StockMovementType.StockOut;
                Assert.All(order.StockMovements, movement =>
                    Assert.Equal(expectedMovementType, movement.Type));
            }
            else
            {
                Assert.Empty(order.StockMovements);
            }
        });

        Assert.Equal(
            dataSet.Products.Count,
            dataSet.Products.Select(product => product.Sku).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            dataSet.Orders.Count,
            dataSet.Orders.Select(order => order.OrderNumber).Distinct(StringComparer.Ordinal).Count());

        var openingStocks = DemoDataCatalog.GetOpeningStockQuantities();
        var movements = dataSet.Orders.SelectMany(order => order.StockMovements).ToList();
        foreach (var product in dataSet.Products)
        {
            var stockIn = movements
                .Where(movement => ReferenceEquals(movement.Product, product)
                    && movement.Type == StockMovementType.StockIn)
                .Sum(movement => movement.Quantity);
            var stockOut = movements
                .Where(movement => ReferenceEquals(movement.Product, product)
                    && movement.Type == StockMovementType.StockOut)
                .Sum(movement => movement.Quantity);

            Assert.Equal(openingStocks[product.Sku] + stockIn - stockOut, product.StockQuantity);
        }
    }
}
