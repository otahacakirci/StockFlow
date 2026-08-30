using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using StockFlow.Data;
using StockFlow.Entities;
using StockFlow.Services.Common;
using StockFlow.Services.Customers;
using StockFlow.Tests.Infrastructure;
using StockFlow.ViewModels.Customers;

namespace StockFlow.Tests.Services;

public sealed class CustomerServiceTests : SqlServerDatabaseTestBase
{
    [Fact]
    public async Task GetListAsync_AppliesContactSearchSortPaginationAndProjectionWithoutTracking()
    {
        await SeedCustomerAsync(new Customer
        {
            Name = "Alpha",
            Email = "alpha@example.com",
            Phone = "+90 555 111 1111"
        });
        await SeedCustomerAsync(new Customer
        {
            Name = "Bravo",
            Email = "office@example.com",
            Phone = "+90 555 222 2222"
        });
        await SeedCustomerAsync(CreateCustomerWithOrders(
            "Office Customer",
            [OrderStatus.Draft, OrderStatus.Cancelled],
            email: "contact@example.com",
            phone: "+90 555 333 4444"));

        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var descending = AssertSuccess(await service.GetListAsync(new CustomerListQueryModel
        {
            SearchTerm = "  office  ",
            SortOrder = CustomerSortOrder.NameDescending,
            Page = 1,
            PageSize = 1
        }));

        Assert.Equal("office", descending.SearchTerm);
        Assert.Equal(CustomerSortOrder.NameDescending, descending.SortOrder);
        Assert.Equal(1, descending.Page);
        Assert.Equal(1, descending.PageSize);
        Assert.Equal(2, descending.TotalCount);
        Assert.Equal(2, descending.TotalPages);
        var firstItem = Assert.Single(descending.Items);
        Assert.Equal("Office Customer", firstItem.Name);
        Assert.Equal(2, firstItem.OrderCount);

        var ascendingSecondPage = AssertSuccess(await service.GetListAsync(new CustomerListQueryModel
        {
            SearchTerm = "office",
            SortOrder = CustomerSortOrder.NameAscending,
            Page = 2,
            PageSize = 1
        }));
        Assert.Equal("Office Customer", Assert.Single(ascendingSecondPage.Items).Name);

        var phoneSearch = AssertSuccess(await service.GetListAsync(new CustomerListQueryModel
        {
            SearchTerm = "4444"
        }));
        Assert.Equal("Office Customer", Assert.Single(phoneSearch.Items).Name);
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task GetListAsync_NormalizesInvalidValuesAndHandlesEmptyResults()
    {
        await SeedCustomerAsync(new Customer { Name = "Beta" });
        await SeedCustomerAsync(new Customer { Name = "Alpha" });

        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var normalized = AssertSuccess(await service.GetListAsync(new CustomerListQueryModel
        {
            SearchTerm = "   ",
            SortOrder = (CustomerSortOrder)int.MaxValue,
            Page = int.MaxValue,
            PageSize = int.MaxValue
        }));

        Assert.Null(normalized.SearchTerm);
        Assert.Equal(CustomerSortOrder.NameAscending, normalized.SortOrder);
        Assert.Equal(1, normalized.Page);
        Assert.Equal(100, normalized.PageSize);
        Assert.Equal(["Alpha", "Beta"], normalized.Items.Select(customer => customer.Name));

        var empty = AssertSuccess(await service.GetListAsync(new CustomerListQueryModel
        {
            SearchTerm = "missing",
            Page = -1,
            PageSize = 0
        }));
        Assert.Equal(1, empty.Page);
        Assert.Equal(20, empty.PageSize);
        Assert.Equal(0, empty.TotalPages);
        Assert.Empty(empty.Items);
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task GetSelectionOptionsAsync_ReturnsOnlySortedIdentityAndNameProjectionWithoutTracking()
    {
        var betaId = await SeedCustomerAsync(new Customer
        {
            Name = "Beta",
            Email = "private@example.com",
            Phone = "+90 555 100 0000",
            Address = "Private address"
        });
        var alphaId = await SeedCustomerAsync(new Customer { Name = "Alpha" });

        await using var dbContext = CreateDbContext();
        var result = await CreateService(dbContext).GetSelectionOptionsAsync();
        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        var options = Assert.IsAssignableFrom<IReadOnlyList<CustomerSelectionOptionViewModel>>(
            result.Value);

        Assert.Equal([alphaId, betaId], options.Select(option => option.Id));
        Assert.Equal(["Alpha", "Beta"], options.Select(option => option.Name));
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsContactProjectionOrderCountAndNotFoundError()
    {
        var customerId = await SeedCustomerAsync(CreateCustomerWithOrders(
            "Detail Customer",
            [OrderStatus.Draft, OrderStatus.Confirmed],
            email: "detail@example.com",
            phone: "+90 555 123 4567",
            address: "Detail address"));

        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var customer = AssertSuccess(await service.GetByIdAsync(customerId));

        Assert.Equal(customerId, customer.Id);
        Assert.Equal("Detail Customer", customer.Name);
        Assert.Equal("detail@example.com", customer.Email);
        Assert.Equal("+90 555 123 4567", customer.Phone);
        Assert.Equal("Detail address", customer.Address);
        Assert.Equal(2, customer.OrderCount);
        Assert.Empty(dbContext.ChangeTracker.Entries());

        AssertFailure(
            await service.GetByIdAsync(int.MaxValue),
            ServiceErrorCategory.NotFound,
            CustomerServiceErrorCodes.CustomerNotFound);
    }

    [Fact]
    public async Task CreateAsync_TrimsValuesAndNormalizesEmptyOptionalFieldsToNull()
    {
        await using var dbContext = CreateDbContext();
        var created = AssertSuccess(await CreateService(dbContext).CreateAsync(new CustomerInputModel
        {
            Name = "  New Customer  ",
            Email = "  new.customer@example.com  ",
            Phone = "   ",
            Address = "  Customer address  "
        }));

        Assert.Equal("New Customer", created.Name);
        Assert.Equal("new.customer@example.com", created.Email);
        Assert.Null(created.Phone);
        Assert.Equal("Customer address", created.Address);
        Assert.Equal(0, created.OrderCount);

        await using var verificationContext = CreateDbContext();
        var stored = await verificationContext.Customers.SingleAsync(
            customer => customer.Id == created.Id);
        Assert.Equal("New Customer", stored.Name);
        Assert.Equal("new.customer@example.com", stored.Email);
        Assert.Null(stored.Phone);
        Assert.Equal("Customer address", stored.Address);
    }

    [Fact]
    public async Task CreateAsync_ReturnsValidationErrorsForInvalidInput()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        AssertFailure(await service.CreateAsync(null), ServiceErrorCategory.Validation, CustomerServiceErrorCodes.InputRequired);
        AssertFailure(await service.CreateAsync(ValidInput(name: " ")), ServiceErrorCategory.Validation, CustomerServiceErrorCodes.NameRequired);
        AssertFailure(await service.CreateAsync(ValidInput(name: new string('n', 151))), ServiceErrorCategory.Validation, CustomerServiceErrorCodes.NameTooLong);
        AssertFailure(await service.CreateAsync(ValidInput(email: new string('e', 257))), ServiceErrorCategory.Validation, CustomerServiceErrorCodes.EmailTooLong);
        AssertFailure(await service.CreateAsync(ValidInput(email: "invalid-email")), ServiceErrorCategory.Validation, CustomerServiceErrorCodes.EmailInvalid);
        AssertFailure(await service.CreateAsync(ValidInput(phone: new string('1', 33))), ServiceErrorCategory.Validation, CustomerServiceErrorCodes.PhoneTooLong);
        AssertFailure(await service.CreateAsync(ValidInput(phone: "not-a-phone")), ServiceErrorCategory.Validation, CustomerServiceErrorCodes.PhoneInvalid);
        AssertFailure(await service.CreateAsync(ValidInput(address: new string('a', 501))), ServiceErrorCategory.Validation, CustomerServiceErrorCodes.AddressTooLong);

        Assert.False(await dbContext.Customers.AnyAsync());
    }

    [Fact]
    public async Task UpdateAsync_NormalizesEditableFieldsAndPreservesOrderHistory()
    {
        var customerId = await SeedCustomerAsync(CreateCustomerWithOrders(
            "Original",
            [OrderStatus.Draft],
            email: "original@example.com",
            phone: "+90 555 000 0000",
            address: "Original address"));

        await using (var updateContext = CreateDbContext())
        {
            var updated = AssertSuccess(await CreateService(updateContext).UpdateAsync(
                customerId,
                new CustomerInputModel
                {
                    Name = "  Updated  ",
                    Email = "   ",
                    Phone = "  +90 555 123 4567  ",
                    Address = "   "
                }));

            Assert.Equal("Updated", updated.Name);
            Assert.Null(updated.Email);
            Assert.Equal("+90 555 123 4567", updated.Phone);
            Assert.Null(updated.Address);
            Assert.Equal(1, updated.OrderCount);
        }

        await using var verificationContext = CreateDbContext();
        var customer = await verificationContext.Customers.SingleAsync(
            candidate => candidate.Id == customerId);
        Assert.Equal("Updated", customer.Name);
        Assert.Null(customer.Email);
        Assert.Equal("+90 555 123 4567", customer.Phone);
        Assert.Null(customer.Address);
        Assert.Equal(1, await verificationContext.Orders.CountAsync(
            order => order.CustomerId == customerId));
    }

    [Fact]
    public async Task UpdateAsync_SeparatesNotFoundAndValidationErrors()
    {
        var customerId = await SeedCustomerAsync(new Customer { Name = "Original" });

        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        AssertFailure(
            await service.UpdateAsync(int.MaxValue, ValidInput()),
            ServiceErrorCategory.NotFound,
            CustomerServiceErrorCodes.CustomerNotFound);
        AssertFailure(
            await service.UpdateAsync(customerId, null),
            ServiceErrorCategory.Validation,
            CustomerServiceErrorCodes.InputRequired);
        AssertFailure(
            await service.UpdateAsync(customerId, ValidInput(email: "invalid-email")),
            ServiceErrorCategory.Validation,
            CustomerServiceErrorCodes.EmailInvalid);

        await using var verificationContext = CreateDbContext();
        Assert.Equal("Original", await verificationContext.Customers
            .Where(customer => customer.Id == customerId)
            .Select(customer => customer.Name)
            .SingleAsync());
    }

    [Fact]
    public async Task DeleteAsync_WhenCustomerHasNoOrders_DeletesCustomerAndReturnsNotFoundAfterward()
    {
        var customerId = await SeedCustomerAsync(new Customer { Name = "Disposable" });

        await using (var deleteContext = CreateDbContext())
        {
            var service = CreateService(deleteContext);
            var deleted = await service.DeleteAsync(customerId);
            Assert.True(deleted.IsSuccess);
            Assert.Null(deleted.Error);

            AssertFailure(
                await service.DeleteAsync(customerId),
                ServiceErrorCategory.NotFound,
                CustomerServiceErrorCodes.CustomerNotFound);
        }

        await using var verificationContext = CreateDbContext();
        Assert.False(await verificationContext.Customers.AnyAsync(
            customer => customer.Id == customerId));
    }

    [Theory]
    [InlineData(OrderStatus.Draft)]
    [InlineData(OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task DeleteAsync_WhenAnyOrderHistoryExists_ReturnsBusinessRuleAndPreservesCustomer(
        OrderStatus orderStatus)
    {
        var customerId = await SeedCustomerAsync(CreateCustomerWithOrders(
            $"Protected {orderStatus}",
            [orderStatus]));

        await using (var deleteContext = CreateDbContext())
        {
            AssertFailure(
                await CreateService(deleteContext).DeleteAsync(customerId),
                ServiceErrorCategory.BusinessRule,
                CustomerServiceErrorCodes.CustomerHasOrders);
        }

        await using var verificationContext = CreateDbContext();
        Assert.True(await verificationContext.Customers.AnyAsync(
            customer => customer.Id == customerId));
        Assert.True(await verificationContext.Orders.AnyAsync(
            order => order.CustomerId == customerId && order.Status == orderStatus));
    }

    [Fact]
    public async Task CreateAsync_WhenPersistenceFails_RethrowsAndClearsTracker()
    {
        await using (var failingContext = CreateDbContext(new ThrowBeforeSaveChangesInterceptor()))
        {
            var service = CreateService(failingContext);
            await Assert.ThrowsAsync<TestPersistenceException>(() => service.CreateAsync(
                ValidInput(name: "Persistence Failure")));
            Assert.Empty(failingContext.ChangeTracker.Entries());
        }

        await using var verificationContext = CreateDbContext();
        Assert.False(await verificationContext.Customers.AnyAsync(
            customer => customer.Name == "Persistence Failure"));
    }

    private async Task<int> SeedCustomerAsync(Customer customer)
    {
        await using var dbContext = CreateDbContext();
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();
        return customer.Id;
    }

    private static Customer CreateCustomerWithOrders(
        string name,
        IReadOnlyCollection<OrderStatus> orderStatuses,
        string? email = null,
        string? phone = null,
        string? address = null)
    {
        var customer = new Customer
        {
            Name = name,
            Email = email,
            Phone = phone,
            Address = address
        };

        foreach (var status in orderStatuses)
        {
            customer.Orders.Add(new Order
            {
                OrderNumber = Guid.NewGuid().ToString("N"),
                Type = OrderType.Sale,
                Status = status,
                OrderDate = DateTime.UtcNow,
                TotalAmount = 1.00m
            });
        }

        return customer;
    }

    private static CustomerInputModel ValidInput(
        string name = "Valid Customer",
        string? email = "valid.customer@example.com",
        string? phone = "+90 555 123 4567",
        string? address = "Valid address")
    {
        return new CustomerInputModel
        {
            Name = name,
            Email = email,
            Phone = phone,
            Address = address
        };
    }

    private static CustomerService CreateService(ApplicationDbContext dbContext)
    {
        return new CustomerService(
            dbContext,
            NullLogger<CustomerService>.Instance);
    }

    private static T AssertSuccess<T>(ServiceResult<T> result)
    {
        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        return Assert.IsType<T>(result.Value);
    }

    private static void AssertFailure<T>(
        ServiceResult<T> result,
        ServiceErrorCategory category,
        string code)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.NotNull(result.Error);
        Assert.Equal(category, result.Error.Category);
        Assert.Equal(code, result.Error.Code);
    }

    private static void AssertFailure(
        ServiceResult result,
        ServiceErrorCategory category,
        string code)
    {
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(category, result.Error.Category);
        Assert.Equal(code, result.Error.Code);
    }

    private sealed class ThrowBeforeSaveChangesInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            throw new TestPersistenceException();
        }
    }

    private sealed class TestPersistenceException : Exception;
}
