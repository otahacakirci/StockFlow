using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StockFlow.Data;
using StockFlow.Entities;
using StockFlow.Options;
using StockFlow.Security;
using StockFlow.Services.Common;
using StockFlow.Services.Dashboard;
using StockFlow.Services.Orders;
using StockFlow.Services.Products;
using StockFlow.Services.StockMovements;
using StockFlow.Tests.Infrastructure;
using StockFlow.ViewModels.Orders;
using StockFlow.ViewModels.Products;
using StockFlow.ViewModels.StockMovements;

namespace StockFlow.Tests.Data;

public sealed class DemoDataSeederTests : SqlServerDatabaseTestBase
{
    [Fact]
    public async Task SeedAsync_DevelopmentEmptyDomain_CreatesConsistentDemoData()
    {
        var creatorUserIds = await SeedIdentityUsersAsync();

        var outcome = await RunSeederAsync(enabled: true, Environments.Development);

        Assert.Equal(DemoDataSeedOutcome.Seeded, outcome);

        await using var dbContext = CreateDbContext();
        Assert.Equal(DemoDataCatalog.ExpectedCategoryCount, await dbContext.Categories.CountAsync());
        Assert.Equal(DemoDataCatalog.ExpectedProductCount, await dbContext.Products.CountAsync());
        Assert.Equal(DemoDataCatalog.ExpectedCustomerCount, await dbContext.Customers.CountAsync());
        Assert.Equal(DemoDataCatalog.ExpectedSupplierCount, await dbContext.Suppliers.CountAsync());
        Assert.Equal(DemoDataCatalog.ExpectedOrderCount, await dbContext.Orders.CountAsync());
        Assert.Equal(DemoDataCatalog.ExpectedOrderItemCount, await dbContext.OrderItems.CountAsync());
        Assert.Equal(DemoDataCatalog.ExpectedStockMovementCount, await dbContext.StockMovements.CountAsync());

        Assert.Equal(14, await dbContext.Orders.CountAsync(order => order.Type == OrderType.Sale));
        Assert.Equal(10, await dbContext.Orders.CountAsync(order => order.Type == OrderType.Purchase));
        Assert.Equal(12, await dbContext.Orders.CountAsync(order => order.Status == OrderStatus.Confirmed));
        Assert.Equal(7, await dbContext.Orders.CountAsync(order => order.Status == OrderStatus.Draft));
        Assert.Equal(5, await dbContext.Orders.CountAsync(order => order.Status == OrderStatus.Cancelled));

        var products = await dbContext.Products.AsNoTracking().ToListAsync();
        var productsById = products.ToDictionary(product => product.Id);
        var orders = await dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .Include(order => order.StockMovements)
            .OrderBy(order => order.OrderDate)
            .ToListAsync();
        var movements = await dbContext.StockMovements.AsNoTracking().ToListAsync();

        Assert.All(orders, order =>
        {
            Assert.Contains(order.CreatedByUserId, creatorUserIds);
            Assert.NotEmpty(order.Items);
            Assert.Equal(order.Items.Count, order.Items.Select(item => item.ProductId).Distinct().Count());
            Assert.Equal(
                order.Items.Sum(item => item.Quantity * item.UnitPrice),
                order.TotalAmount);

            if (order.Type == OrderType.Sale)
            {
                Assert.NotNull(order.CustomerId);
                Assert.Null(order.SupplierId);
            }
            else
            {
                Assert.Null(order.CustomerId);
                Assert.NotNull(order.SupplierId);
            }

            Assert.All(order.Items, item =>
            {
                Assert.True(item.Quantity > 0);
                Assert.Equal(productsById[item.ProductId].Price, item.UnitPrice);
            });

            if (order.Status == OrderStatus.Confirmed)
            {
                var expectedMovementType = order.Type == OrderType.Purchase
                    ? StockMovementType.StockIn
                    : StockMovementType.StockOut;
                Assert.Equal(order.Items.Count, order.StockMovements.Count);

                foreach (var item in order.Items)
                {
                    var movement = Assert.Single(
                        order.StockMovements,
                        candidate => candidate.ProductId == item.ProductId);
                    Assert.Equal(expectedMovementType, movement.Type);
                    Assert.Equal(item.Quantity, movement.Quantity);
                    Assert.True(movement.Quantity > 0);
                }
            }
            else
            {
                Assert.Empty(order.StockMovements);
            }
        });

        var openingStocks = DemoDataCatalog.GetOpeningStockQuantities();
        foreach (var product in products)
        {
            var stockIn = movements
                .Where(movement => movement.ProductId == product.Id
                    && movement.Type == StockMovementType.StockIn)
                .Sum(movement => movement.Quantity);
            var stockOut = movements
                .Where(movement => movement.ProductId == product.Id
                    && movement.Type == StockMovementType.StockOut)
                .Sum(movement => movement.Quantity);

            Assert.Equal(openingStocks[product.Sku] + stockIn - stockOut, product.StockQuantity);
            Assert.True(product.StockQuantity >= 0);
        }

        Assert.Equal(
            DemoDataCatalog.ExpectedLowStockProductCount,
            products.Count(product => product.StockQuantity <= product.MinimumStockQuantity));
        Assert.Equal(new DateTime(2026, 6, 2, 9, 0, 0, DateTimeKind.Utc), orders.First().OrderDate);
        Assert.Equal(new DateTime(2026, 8, 30, 9, 0, 0, DateTimeKind.Utc), orders.Last().OrderDate);

        var customers = await dbContext.Customers.AsNoTracking().ToListAsync();
        Assert.All(customers, customer =>
        {
            Assert.EndsWith("@example.com", customer.Email, StringComparison.Ordinal);
            Assert.Contains("000", customer.Phone, StringComparison.Ordinal);
            Assert.Contains("Kurgusal", customer.Address, StringComparison.Ordinal);
        });
        var suppliers = await dbContext.Suppliers.AsNoTracking().ToListAsync();
        Assert.All(suppliers, supplier =>
        {
            Assert.EndsWith("@example.org", supplier.Email, StringComparison.Ordinal);
            Assert.Contains("000", supplier.Phone, StringComparison.Ordinal);
            Assert.Contains("Kurgusal", supplier.Address, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task SeedAsync_WhenRunTwice_DoesNotDuplicateOrChangeData()
    {
        await SeedIdentityUsersAsync();
        Assert.Equal(
            DemoDataSeedOutcome.Seeded,
            await RunSeederAsync(enabled: true, Environments.Development));

        DemoCounts beforeCounts;
        IReadOnlyDictionary<string, int> beforeStocks;
        await using (var dbContext = CreateDbContext())
        {
            beforeCounts = await ReadCountsAsync(dbContext);
            beforeStocks = await dbContext.Products
                .AsNoTracking()
                .ToDictionaryAsync(product => product.Sku, product => product.StockQuantity);
        }

        Assert.Equal(
            DemoDataSeedOutcome.ExistingDomainData,
            await RunSeederAsync(enabled: true, Environments.Development));

        await using var verificationContext = CreateDbContext();
        Assert.Equal(beforeCounts, await ReadCountsAsync(verificationContext));
        var afterStocks = await verificationContext.Products
            .AsNoTracking()
            .ToDictionaryAsync(product => product.Sku, product => product.StockQuantity);
        Assert.Equal(beforeStocks.OrderBy(pair => pair.Key), afterStocks.OrderBy(pair => pair.Key));
    }

    [Fact]
    public async Task SeedAsync_WhenDomainDataExists_PreservesItAndWritesSafeSkipLog()
    {
        await SeedIdentityUsersAsync();
        await using (var setupContext = CreateDbContext())
        {
            setupContext.Categories.Add(new Category { Name = "Kullanıcı Kategorisi" });
            await setupContext.SaveChangesAsync();
        }

        var logger = new ListLogger<DemoDataSeeder>();
        var outcome = await RunSeederAsync(
            enabled: true,
            Environments.Development,
            logger);

        Assert.Equal(DemoDataSeedOutcome.ExistingDomainData, outcome);
        Assert.Contains(
            logger.Messages,
            message => message.Contains("mevcut domain verisi", StringComparison.Ordinal));

        await using var verificationContext = CreateDbContext();
        Assert.Equal(["Kullanıcı Kategorisi"], await verificationContext.Categories
            .AsNoTracking()
            .Select(category => category.Name)
            .ToListAsync());
        Assert.Equal(
            DemoCounts.Empty with { Categories = 1 },
            await ReadCountsAsync(verificationContext));
    }

    [Fact]
    public async Task SeedAsync_CreatesEnoughDataForDashboardFiltersAndPaging()
    {
        await SeedIdentityUsersAsync();
        Assert.Equal(
            DemoDataSeedOutcome.Seeded,
            await RunSeederAsync(enabled: true, Environments.Development));

        await using var dbContext = CreateDbContext();
        var dashboard = AssertSuccess(await new DashboardService(dbContext).GetAsync());
        Assert.Equal(DemoDataCatalog.ExpectedProductCount, dashboard.TotalProductCount);
        Assert.Equal(DemoDataCatalog.ExpectedLowStockProductCount, dashboard.LowStockProductCount);
        Assert.Equal(DemoDataCatalog.ExpectedCustomerCount, dashboard.TotalCustomerCount);
        Assert.Equal(DemoDataCatalog.ExpectedSupplierCount, dashboard.TotalSupplierCount);
        Assert.Equal(DemoDataCatalog.ExpectedOrderCount, dashboard.TotalOrderCount);
        Assert.True(dashboard.ConfirmedSaleTotalAmount > 0);
        Assert.Equal(5, dashboard.RecentOrders.Count);

        var productService = new ProductService(
            dbContext,
            NullLogger<ProductService>.Instance);
        var products = AssertSuccess(await productService.GetListAsync());
        Assert.Equal(2, products.TotalPages);
        Assert.Equal(DemoDataCatalog.ExpectedProductCount, products.TotalCount);
        var technologyProducts = AssertSuccess(await productService.GetListAsync(
            new ProductListQueryModel { SearchTerm = "DEMO-TEC" }));
        Assert.Equal(5, technologyProducts.TotalCount);
        var lowStockProducts = AssertSuccess(await productService.GetListAsync(
            new ProductListQueryModel { LowStockOnly = true }));
        Assert.Equal(DemoDataCatalog.ExpectedLowStockProductCount, lowStockProducts.TotalCount);

        var orderService = new OrderQueryService(dbContext);
        var orders = AssertSuccess(await orderService.GetListAsync());
        Assert.Equal(2, orders.TotalPages);
        Assert.Equal(DemoDataCatalog.ExpectedOrderCount, orders.TotalCount);
        var draftPurchases = AssertSuccess(await orderService.GetListAsync(
            new OrderListQueryModel
            {
                Type = OrderType.Purchase,
                Status = OrderStatus.Draft
            }));
        Assert.Equal(3, draftPurchases.TotalCount);

        var movementService = new StockMovementQueryService(dbContext);
        var stockMovements = AssertSuccess(await movementService.GetListAsync());
        Assert.Equal(2, stockMovements.TotalPages);
        Assert.Equal(DemoDataCatalog.ExpectedStockMovementCount, stockMovements.TotalCount);
        var stockInMovements = AssertSuccess(await movementService.GetListAsync(
            new StockMovementListQueryModel { Type = StockMovementType.StockIn }));
        Assert.Equal(13, stockInMovements.TotalCount);
        var stockOutMovements = AssertSuccess(await movementService.GetListAsync(
            new StockMovementListQueryModel { Type = StockMovementType.StockOut }));
        Assert.Equal(17, stockOutMovements.TotalCount);
    }

    private async Task<IReadOnlyCollection<string>> SeedIdentityUsersAsync()
    {
        await using var dbContext = CreateDbContext();
        var adminRole = new IdentityRole
        {
            Id = "demo-admin-role",
            Name = AppRoles.Admin,
            NormalizedName = AppRoles.Admin.ToUpperInvariant()
        };
        var employeeRole = new IdentityRole
        {
            Id = "demo-employee-role",
            Name = AppRoles.Employee,
            NormalizedName = AppRoles.Employee.ToUpperInvariant()
        };
        var adminUser = CreateUser("demo-admin-user", "admin@stockflow.test");
        var employeeUser = CreateUser("demo-employee-user", "employee@stockflow.test");

        dbContext.Roles.AddRange(adminRole, employeeRole);
        dbContext.Users.AddRange(adminUser, employeeUser);
        dbContext.UserRoles.AddRange(
            new IdentityUserRole<string>
            {
                RoleId = adminRole.Id,
                UserId = adminUser.Id
            },
            new IdentityUserRole<string>
            {
                RoleId = employeeRole.Id,
                UserId = employeeUser.Id
            });
        await dbContext.SaveChangesAsync();

        return [adminUser.Id, employeeUser.Id];
    }

    private async Task<DemoDataSeedOutcome> RunSeederAsync(
        bool enabled,
        string environmentName,
        ILogger<DemoDataSeeder>? logger = null)
    {
        await using var dbContext = CreateDbContext();
        var seeder = new DemoDataSeeder(
            dbContext,
            new TestHostEnvironment(environmentName),
            Microsoft.Extensions.Options.Options.Create(
                new DemoDataOptions { Enabled = enabled }),
            Microsoft.Extensions.Options.Options.Create(new IdentitySeedOptions
            {
                Admin = new IdentitySeedUserOptions { Email = "admin@stockflow.test" },
                Employee = new IdentitySeedUserOptions { Email = "employee@stockflow.test" }
            }),
            logger ?? NullLogger<DemoDataSeeder>.Instance);
        return await seeder.SeedAsync();
    }

    private static ApplicationUser CreateUser(string id, string email)
    {
        return new ApplicationUser
        {
            Id = id,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            SecurityStamp = $"security-{id}",
            ConcurrencyStamp = $"concurrency-{id}"
        };
    }

    private static async Task<DemoCounts> ReadCountsAsync(ApplicationDbContext dbContext)
    {
        return new DemoCounts(
            await dbContext.Categories.CountAsync(),
            await dbContext.Products.CountAsync(),
            await dbContext.Customers.CountAsync(),
            await dbContext.Suppliers.CountAsync(),
            await dbContext.Orders.CountAsync(),
            await dbContext.OrderItems.CountAsync(),
            await dbContext.StockMovements.CountAsync());
    }

    private static T AssertSuccess<T>(ServiceResult<T> result)
    {
        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        return Assert.IsType<T>(result.Value);
    }

    private sealed record DemoCounts(
        int Categories,
        int Products,
        int Customers,
        int Suppliers,
        int Orders,
        int OrderItems,
        int StockMovements)
    {
        public static DemoCounts Empty { get; } = new(0, 0, 0, 0, 0, 0, 0);
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "StockFlow.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public IList<string> Messages { get; } = new List<string>();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
