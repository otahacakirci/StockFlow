using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using StockFlow.Data;
using StockFlow.Entities;
using StockFlow.Services.Common;
using StockFlow.Services.Suppliers;
using StockFlow.Tests.Infrastructure;
using StockFlow.ViewModels.Suppliers;

namespace StockFlow.Tests.Services;

public sealed class SupplierServiceTests : SqlServerDatabaseTestBase
{
    [Fact]
    public async Task GetListAsync_AppliesContactSearchSortPaginationAndProjectionWithoutTracking()
    {
        await SeedSupplierAsync(new Supplier
        {
            CompanyName = "Alpha Supply",
            Email = "alpha@example.com",
            Phone = "+90 555 111 1111"
        });
        await SeedSupplierAsync(new Supplier
        {
            CompanyName = "Bravo Trade",
            Email = "office@example.com",
            Phone = "+90 555 222 2222"
        });
        await SeedSupplierAsync(CreateSupplierWithOrders(
            "Office Supplier",
            [OrderStatus.Draft, OrderStatus.Cancelled],
            email: "contact@example.com",
            phone: "+90 555 333 4444"));

        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var descending = AssertSuccess(await service.GetListAsync(new SupplierListQueryModel
        {
            SearchTerm = "  office  ",
            SortOrder = SupplierSortOrder.CompanyNameDescending,
            Page = 1,
            PageSize = 1
        }));

        Assert.Equal("office", descending.SearchTerm);
        Assert.Equal(SupplierSortOrder.CompanyNameDescending, descending.SortOrder);
        Assert.Equal(1, descending.Page);
        Assert.Equal(1, descending.PageSize);
        Assert.Equal(2, descending.TotalCount);
        Assert.Equal(2, descending.TotalPages);
        var firstItem = Assert.Single(descending.Items);
        Assert.Equal("Office Supplier", firstItem.CompanyName);
        Assert.Equal(2, firstItem.OrderCount);

        var ascendingSecondPage = AssertSuccess(await service.GetListAsync(new SupplierListQueryModel
        {
            SearchTerm = "office",
            SortOrder = SupplierSortOrder.CompanyNameAscending,
            Page = 2,
            PageSize = 1
        }));
        Assert.Equal("Office Supplier", Assert.Single(ascendingSecondPage.Items).CompanyName);

        var phoneSearch = AssertSuccess(await service.GetListAsync(new SupplierListQueryModel
        {
            SearchTerm = "4444"
        }));
        Assert.Equal("Office Supplier", Assert.Single(phoneSearch.Items).CompanyName);
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task GetListAsync_NormalizesInvalidValuesAndHandlesEmptyResults()
    {
        await SeedSupplierAsync(new Supplier { CompanyName = "Beta Supply" });
        await SeedSupplierAsync(new Supplier { CompanyName = "Alpha Supply" });

        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var normalized = AssertSuccess(await service.GetListAsync(new SupplierListQueryModel
        {
            SearchTerm = "   ",
            SortOrder = (SupplierSortOrder)int.MaxValue,
            Page = int.MaxValue,
            PageSize = int.MaxValue
        }));

        Assert.Null(normalized.SearchTerm);
        Assert.Equal(SupplierSortOrder.CompanyNameAscending, normalized.SortOrder);
        Assert.Equal(1, normalized.Page);
        Assert.Equal(100, normalized.PageSize);
        Assert.Equal(
            ["Alpha Supply", "Beta Supply"],
            normalized.Items.Select(supplier => supplier.CompanyName));

        var empty = AssertSuccess(await service.GetListAsync(new SupplierListQueryModel
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
    public async Task GetSelectionOptionsAsync_ReturnsOnlySortedIdentityAndCompanyNameProjectionWithoutTracking()
    {
        var betaId = await SeedSupplierAsync(new Supplier
        {
            CompanyName = "Beta Supply",
            Email = "private@example.com",
            Phone = "+90 555 100 0000",
            Address = "Private address"
        });
        var alphaId = await SeedSupplierAsync(new Supplier { CompanyName = "Alpha Supply" });

        await using var dbContext = CreateDbContext();
        var result = await CreateService(dbContext).GetSelectionOptionsAsync();
        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        var options = Assert.IsAssignableFrom<IReadOnlyList<SupplierSelectionOptionViewModel>>(
            result.Value);

        Assert.Equal([alphaId, betaId], options.Select(option => option.Id));
        Assert.Equal(
            ["Alpha Supply", "Beta Supply"],
            options.Select(option => option.CompanyName));
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsContactProjectionOrderCountAndNotFoundError()
    {
        var supplierId = await SeedSupplierAsync(CreateSupplierWithOrders(
            "Detail Supplier",
            [OrderStatus.Draft, OrderStatus.Confirmed],
            email: "detail@example.com",
            phone: "+90 555 123 4567",
            address: "Detail address"));

        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var supplier = AssertSuccess(await service.GetByIdAsync(supplierId));

        Assert.Equal(supplierId, supplier.Id);
        Assert.Equal("Detail Supplier", supplier.CompanyName);
        Assert.Equal("detail@example.com", supplier.Email);
        Assert.Equal("+90 555 123 4567", supplier.Phone);
        Assert.Equal("Detail address", supplier.Address);
        Assert.Equal(2, supplier.OrderCount);
        Assert.Empty(dbContext.ChangeTracker.Entries());

        AssertFailure(
            await service.GetByIdAsync(int.MaxValue),
            ServiceErrorCategory.NotFound,
            SupplierServiceErrorCodes.SupplierNotFound);
    }

    [Fact]
    public async Task CreateAsync_TrimsValuesAndNormalizesEmptyOptionalFieldsToNull()
    {
        await using var dbContext = CreateDbContext();
        var created = AssertSuccess(await CreateService(dbContext).CreateAsync(new SupplierInputModel
        {
            CompanyName = "  New Supplier  ",
            Email = "  new.supplier@example.com  ",
            Phone = "   ",
            Address = "  Supplier address  "
        }));

        Assert.Equal("New Supplier", created.CompanyName);
        Assert.Equal("new.supplier@example.com", created.Email);
        Assert.Null(created.Phone);
        Assert.Equal("Supplier address", created.Address);
        Assert.Equal(0, created.OrderCount);

        await using var verificationContext = CreateDbContext();
        var stored = await verificationContext.Suppliers.SingleAsync(
            supplier => supplier.Id == created.Id);
        Assert.Equal("New Supplier", stored.CompanyName);
        Assert.Equal("new.supplier@example.com", stored.Email);
        Assert.Null(stored.Phone);
        Assert.Equal("Supplier address", stored.Address);
    }

    [Fact]
    public async Task CreateAsync_ReturnsValidationErrorsForInvalidInput()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        AssertFailure(await service.CreateAsync(null), ServiceErrorCategory.Validation, SupplierServiceErrorCodes.InputRequired);
        AssertFailure(await service.CreateAsync(ValidInput(companyName: " ")), ServiceErrorCategory.Validation, SupplierServiceErrorCodes.CompanyNameRequired);
        AssertFailure(await service.CreateAsync(ValidInput(companyName: new string('n', 201))), ServiceErrorCategory.Validation, SupplierServiceErrorCodes.CompanyNameTooLong);
        AssertFailure(await service.CreateAsync(ValidInput(email: new string('e', 257))), ServiceErrorCategory.Validation, SupplierServiceErrorCodes.EmailTooLong);
        AssertFailure(await service.CreateAsync(ValidInput(email: "invalid-email")), ServiceErrorCategory.Validation, SupplierServiceErrorCodes.EmailInvalid);
        AssertFailure(await service.CreateAsync(ValidInput(phone: new string('1', 33))), ServiceErrorCategory.Validation, SupplierServiceErrorCodes.PhoneTooLong);
        AssertFailure(await service.CreateAsync(ValidInput(phone: "not-a-phone")), ServiceErrorCategory.Validation, SupplierServiceErrorCodes.PhoneInvalid);
        AssertFailure(await service.CreateAsync(ValidInput(address: new string('a', 501))), ServiceErrorCategory.Validation, SupplierServiceErrorCodes.AddressTooLong);

        Assert.False(await dbContext.Suppliers.AnyAsync());
    }

    [Fact]
    public async Task UpdateAsync_NormalizesEditableFieldsAndPreservesOrderHistory()
    {
        var supplierId = await SeedSupplierAsync(CreateSupplierWithOrders(
            "Original Supplier",
            [OrderStatus.Draft],
            email: "original@example.com",
            phone: "+90 555 000 0000",
            address: "Original address"));

        await using (var updateContext = CreateDbContext())
        {
            var updated = AssertSuccess(await CreateService(updateContext).UpdateAsync(
                supplierId,
                new SupplierInputModel
                {
                    CompanyName = "  Updated Supplier  ",
                    Email = "   ",
                    Phone = "  +90 555 123 4567  ",
                    Address = "   "
                }));

            Assert.Equal("Updated Supplier", updated.CompanyName);
            Assert.Null(updated.Email);
            Assert.Equal("+90 555 123 4567", updated.Phone);
            Assert.Null(updated.Address);
            Assert.Equal(1, updated.OrderCount);
        }

        await using var verificationContext = CreateDbContext();
        var supplier = await verificationContext.Suppliers.SingleAsync(
            candidate => candidate.Id == supplierId);
        Assert.Equal("Updated Supplier", supplier.CompanyName);
        Assert.Null(supplier.Email);
        Assert.Equal("+90 555 123 4567", supplier.Phone);
        Assert.Null(supplier.Address);
        Assert.Equal(1, await verificationContext.Orders.CountAsync(
            order => order.SupplierId == supplierId));
    }

    [Fact]
    public async Task UpdateAsync_SeparatesNotFoundAndValidationErrors()
    {
        var supplierId = await SeedSupplierAsync(new Supplier { CompanyName = "Original Supplier" });

        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        AssertFailure(
            await service.UpdateAsync(int.MaxValue, ValidInput()),
            ServiceErrorCategory.NotFound,
            SupplierServiceErrorCodes.SupplierNotFound);
        AssertFailure(
            await service.UpdateAsync(supplierId, null),
            ServiceErrorCategory.Validation,
            SupplierServiceErrorCodes.InputRequired);
        AssertFailure(
            await service.UpdateAsync(supplierId, ValidInput(email: "invalid-email")),
            ServiceErrorCategory.Validation,
            SupplierServiceErrorCodes.EmailInvalid);

        await using var verificationContext = CreateDbContext();
        Assert.Equal("Original Supplier", await verificationContext.Suppliers
            .Where(supplier => supplier.Id == supplierId)
            .Select(supplier => supplier.CompanyName)
            .SingleAsync());
    }

    [Fact]
    public async Task DeleteAsync_WhenSupplierHasNoOrders_DeletesSupplierAndReturnsNotFoundAfterward()
    {
        var supplierId = await SeedSupplierAsync(new Supplier { CompanyName = "Disposable Supplier" });

        await using (var deleteContext = CreateDbContext())
        {
            var service = CreateService(deleteContext);
            var deleted = await service.DeleteAsync(supplierId);
            Assert.True(deleted.IsSuccess);
            Assert.Null(deleted.Error);

            AssertFailure(
                await service.DeleteAsync(supplierId),
                ServiceErrorCategory.NotFound,
                SupplierServiceErrorCodes.SupplierNotFound);
        }

        await using var verificationContext = CreateDbContext();
        Assert.False(await verificationContext.Suppliers.AnyAsync(
            supplier => supplier.Id == supplierId));
    }

    [Theory]
    [InlineData(OrderStatus.Draft)]
    [InlineData(OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task DeleteAsync_WhenAnyOrderHistoryExists_ReturnsBusinessRuleAndPreservesSupplier(
        OrderStatus orderStatus)
    {
        var supplierId = await SeedSupplierAsync(CreateSupplierWithOrders(
            $"Protected {orderStatus}",
            [orderStatus]));

        await using (var deleteContext = CreateDbContext())
        {
            AssertFailure(
                await CreateService(deleteContext).DeleteAsync(supplierId),
                ServiceErrorCategory.BusinessRule,
                SupplierServiceErrorCodes.SupplierHasOrders);
        }

        await using var verificationContext = CreateDbContext();
        Assert.True(await verificationContext.Suppliers.AnyAsync(
            supplier => supplier.Id == supplierId));
        Assert.True(await verificationContext.Orders.AnyAsync(
            order => order.SupplierId == supplierId && order.Status == orderStatus));
    }

    [Fact]
    public async Task CreateAsync_WhenPersistenceFails_RethrowsAndClearsTracker()
    {
        await using (var failingContext = CreateDbContext(new ThrowBeforeSaveChangesInterceptor()))
        {
            var service = CreateService(failingContext);
            await Assert.ThrowsAsync<TestPersistenceException>(() => service.CreateAsync(
                ValidInput(companyName: "Persistence Failure")));
            Assert.Empty(failingContext.ChangeTracker.Entries());
        }

        await using var verificationContext = CreateDbContext();
        Assert.False(await verificationContext.Suppliers.AnyAsync(
            supplier => supplier.CompanyName == "Persistence Failure"));
    }

    private async Task<int> SeedSupplierAsync(Supplier supplier)
    {
        await using var dbContext = CreateDbContext();
        dbContext.Suppliers.Add(supplier);
        await dbContext.SaveChangesAsync();
        return supplier.Id;
    }

    private static Supplier CreateSupplierWithOrders(
        string companyName,
        IReadOnlyCollection<OrderStatus> orderStatuses,
        string? email = null,
        string? phone = null,
        string? address = null)
    {
        var supplier = new Supplier
        {
            CompanyName = companyName,
            Email = email,
            Phone = phone,
            Address = address
        };

        foreach (var status in orderStatuses)
        {
            supplier.Orders.Add(new Order
            {
                OrderNumber = Guid.NewGuid().ToString("N"),
                Type = OrderType.Purchase,
                Status = status,
                OrderDate = DateTime.UtcNow,
                TotalAmount = 1.00m
            });
        }

        return supplier;
    }

    private static SupplierInputModel ValidInput(
        string companyName = "Valid Supplier",
        string? email = "valid.supplier@example.com",
        string? phone = "+90 555 123 4567",
        string? address = "Valid address")
    {
        return new SupplierInputModel
        {
            CompanyName = companyName,
            Email = email,
            Phone = phone,
            Address = address
        };
    }

    private static SupplierService CreateService(ApplicationDbContext dbContext)
    {
        return new SupplierService(
            dbContext,
            NullLogger<SupplierService>.Instance);
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
