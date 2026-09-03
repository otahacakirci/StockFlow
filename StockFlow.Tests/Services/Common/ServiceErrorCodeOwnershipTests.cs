using StockFlow.Services.Categories;
using StockFlow.Services.Customers;
using StockFlow.Services.Orders;
using StockFlow.Services.Products;
using StockFlow.Services.Suppliers;

namespace StockFlow.Tests.Services.Common;

public sealed class ServiceErrorCodeOwnershipTests
{
    [Fact]
    public void CompatibilityAliases_UseCanonicalDomainErrorCodes()
    {
        Assert.Equal(
            CategoryServiceErrorCodes.CategoryNotFound,
            ProductServiceErrorCodes.CategoryNotFound);
        Assert.Equal(
            CustomerServiceErrorCodes.CustomerNotFound,
            OrderServiceErrorCodes.CustomerNotFound);
        Assert.Equal(
            SupplierServiceErrorCodes.SupplierNotFound,
            OrderServiceErrorCodes.SupplierNotFound);
        Assert.Equal(
            ProductServiceErrorCodes.ProductNotFound,
            OrderServiceErrorCodes.ProductNotFound);
        Assert.Equal(
            ProductServiceErrorCodes.PriceInvalid,
            OrderServiceErrorCodes.InvalidProductPrice);
    }
}
