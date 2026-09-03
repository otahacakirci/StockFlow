using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using StockFlow.Data;
using StockFlow.Entities;
using StockFlow.Services.Common;
using StockFlow.Services.Customers;
using StockFlow.Services.Products;
using StockFlow.Services.Suppliers;
using StockFlow.ViewModels.Orders;

namespace StockFlow.Services.Orders;

/// <summary>
/// Draft sipariş yaşam döngüsünü, sunucu fiyat güvenliğini ve atomik stok/hareket kalıcılığını yöneten uygulama Service'idir.
/// </summary>
internal sealed class OrderService : IOrderService
{
    private const decimal MaximumDatabaseAmount = 9_999_999_999_999_999.99m;
    private readonly ApplicationDbContext dbContext;
    private readonly ILogger<OrderService> logger;
    private readonly TimeProvider timeProvider;
    private readonly OrderStockConfirmationPlanner stockConfirmationPlanner;

    /// <summary>
    /// Sipariş akışını veritabanı, yapılandırılmış log, UTC zaman kaynağı ve saf stok planner bağımlılıklarıyla oluşturur.
    /// </summary>
    public OrderService(
        ApplicationDbContext dbContext,
        ILogger<OrderService> logger,
        TimeProvider timeProvider,
        OrderStockConfirmationPlanner stockConfirmationPlanner)
    {
        this.dbContext = dbContext;
        this.logger = logger;
        this.timeProvider = timeProvider;
        this.stockConfirmationPlanner = stockConfirmationPlanner;
    }

    /// <summary>
    /// Doğrulanmış kullanıcı ve doğru Sale/Purchase tarafıyla, ürünlerin güncel fiyatlarını snapshot alarak Draft oluşturur. Başarıda sunucu sipariş sonucunu, beklenen giriş veya kayıt sorununda kategorili hata döndürür; stok değiştirmez.
    /// </summary>
    public async Task<ServiceResult<OrderMutationResult>> CreateDraftAsync(
        OrderDraftInputModel input,
        string createdByUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(createdByUserId))
        {
            return Failure(
                ServiceErrorCategory.Validation,
                OrderServiceErrorCodes.CreatorRequired,
                "Siparişi oluşturan kullanıcı bilgisi zorunludur.");
        }

        if (!await dbContext.Users.AnyAsync(
                user => user.Id == createdByUserId,
                cancellationToken))
        {
            return Failure(
                ServiceErrorCategory.NotFound,
                OrderServiceErrorCodes.UserNotFound,
                "Siparişi oluşturan kullanıcı bulunamadı.");
        }

        var validation = await ValidateDraftInputAsync(
            input,
            existingPrices: null,
            cancellationToken);

        if (!validation.IsSuccess)
        {
            logger.LogWarning(
                "Draft order creation rejected with error code {ErrorCode}.",
                validation.Error!.Code);
            return ServiceResult<OrderMutationResult>.Failure(validation.Error!);
        }

        var validatedDraft = validation.Value!;
        var order = CreateDraftOrder(input, validatedDraft, createdByUserId);

        dbContext.Orders.Add(order);
        await PersistChangesAsync("create", order, cancellationToken);

        logger.LogInformation(
            "Draft order {OrderId} created with order number {OrderNumber} and {ItemCount} items.",
            order.Id,
            order.OrderNumber,
            order.Items.Count);

        return ServiceResult<OrderMutationResult>.Success(ToMutationResult(order));
    }

    /// <summary>
    /// Yalnız Draft siparişin tarafını ve kalemlerini eşitler; kalan satırların snapshot fiyatını korur, yalnız yeni ürünlerde güncel fiyat kullanır. Eksik ya da terminal siparişte beklenen hata döndürür ve stok hareketi üretmez.
    /// </summary>
    public async Task<ServiceResult<OrderMutationResult>> UpdateDraftAsync(
        int orderId,
        OrderDraftInputModel input,
        CancellationToken cancellationToken = default)
    {
        var order = await dbContext.Orders
            .Include(candidate => candidate.Items)
            .SingleOrDefaultAsync(candidate => candidate.Id == orderId, cancellationToken);

        if (order is null)
        {
            return OrderNotFound();
        }

        if (order.Status != OrderStatus.Draft)
        {
            return OrderNotDraft(order, "update");
        }

        var existingPrices = order.Items.ToDictionary(
            item => item.ProductId,
            item => item.UnitPrice);
        var validation = await ValidateDraftInputAsync(
            input,
            existingPrices,
            cancellationToken);

        if (!validation.IsSuccess)
        {
            logger.LogWarning(
                "Draft order {OrderId} update rejected with error code {ErrorCode}.",
                order.Id,
                validation.Error!.Code);
            return ServiceResult<OrderMutationResult>.Failure(validation.Error!);
        }

        var validatedDraft = validation.Value!;
        ApplyDraftUpdate(order, input, validatedDraft);
        await PersistChangesAsync("update", order, cancellationToken);

        logger.LogInformation(
            "Draft order {OrderId} updated with {ItemCount} items.",
            order.Id,
            validatedDraft.Items.Count);

        return ServiceResult<OrderMutationResult>.Success(ToMutationResult(order));
    }

    /// <summary>
    /// Kalıcı Draft doğrulaması ve tüm Sale/Purchase stok kontrolleri tamamlandıktan sonra stokları, hareketleri ve Confirmed durumunu tek transaction ve tek kayıt çağrısıyla uygular. Beklenen ihlalde rollback ile hata sonucu döner; beklenmeyen hatada rollback ve tracker temizliğinden sonra hatayı yeniden fırlatır.
    /// </summary>
    public async Task<ServiceResult<OrderMutationResult>> ConfirmDraftAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var order = await dbContext.Orders
                .Include(candidate => candidate.Items)
                .ThenInclude(item => item.Product)
                .SingleOrDefaultAsync(candidate => candidate.Id == orderId, cancellationToken);

            if (order is null)
            {
                return await RollbackFailureAsync(transaction, OrderNotFound(), cancellationToken);
            }

            if (order.Status != OrderStatus.Draft)
            {
                return await RollbackFailureAsync(
                    transaction,
                    OrderNotDraft(order, "confirm"),
                    cancellationToken);
            }

            var persistedValidation = await ValidatePersistedDraftAsync(order, cancellationToken);
            if (!persistedValidation.IsSuccess)
            {
                return await RollbackFailureAsync(
                    transaction,
                    ServiceResult<OrderMutationResult>.Failure(persistedValidation.Error!),
                    cancellationToken);
            }

            var stockDecision = stockConfirmationPlanner.CreatePlan(
                order.Type,
                order.Items.Select(ToStockConfirmationItem).ToList());
            if (!stockDecision.IsApproved)
            {
                return await RollbackFailureAsync(
                    transaction,
                    CreateStockPlanningFailure(order.Id, stockDecision.Failure!),
                    cancellationToken);
            }

            var stockPlan = stockDecision.Plan!;
            ApplyConfirmationPlan(order, persistedValidation.Value, stockPlan);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            LogCommittedStockMovements(order, stockPlan.MovementType);

            logger.LogInformation(
                "Order {OrderId} confirmed as {OrderType} with {ItemCount} stock movements.",
                order.Id,
                order.Type,
                order.Items.Count);

            return ServiceResult<OrderMutationResult>.Success(ToMutationResult(order));
        }
        catch (OperationCanceledException)
        {
            await RollbackAfterUnexpectedFailureAsync(transaction, orderId);
            dbContext.ChangeTracker.Clear();
            throw;
        }
        catch (Exception exception)
        {
            await RollbackAfterUnexpectedFailureAsync(transaction, orderId);
            logger.LogError(exception, "Order {OrderId} confirmation failed.", orderId);
            dbContext.ChangeTracker.Clear();
            throw;
        }
    }

    /// <summary>
    /// Yalnız Draft siparişi stok veya hareket üretmeden terminal Cancelled durumuna geçirir. Eksik ya da terminal siparişte kategorili hata döndürür.
    /// </summary>
    public async Task<ServiceResult<OrderMutationResult>> CancelDraftAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await dbContext.Orders
            .SingleOrDefaultAsync(candidate => candidate.Id == orderId, cancellationToken);

        if (order is null)
        {
            return OrderNotFound();
        }

        if (order.Status != OrderStatus.Draft)
        {
            return OrderNotDraft(order, "cancel");
        }

        order.Status = OrderStatus.Cancelled;

        await PersistChangesAsync("cancel", order, cancellationToken);

        logger.LogInformation("Draft order {OrderId} cancelled.", order.Id);
        return ServiceResult<OrderMutationResult>.Success(ToMutationResult(order));
    }

    /// <summary>
    /// Yalnız StockMovement geçmişi bulunmayan Draft siparişi kalemleriyle birlikte fiziksel olarak siler. Eksik, terminal veya hareket geçmişi bulunan siparişte beklenen hata döndürür.
    /// </summary>
    public async Task<ServiceResult> DeleteDraftAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await dbContext.Orders
            .Include(candidate => candidate.Items)
            .SingleOrDefaultAsync(candidate => candidate.Id == orderId, cancellationToken);

        if (order is null)
        {
            return ServiceResult.Failure(CreateError(
                ServiceErrorCategory.NotFound,
                OrderServiceErrorCodes.OrderNotFound,
                "Sipariş bulunamadı."));
        }

        if (order.Status != OrderStatus.Draft)
        {
            LogStateViolation(order, "delete");
            return ServiceResult.Failure(CreateError(
                ServiceErrorCategory.BusinessRule,
                OrderServiceErrorCodes.OrderNotDraft,
                "Yalnızca taslak siparişler silinebilir."));
        }

        if (await dbContext.StockMovements.AnyAsync(
                movement => movement.OrderId == orderId,
                cancellationToken))
        {
            logger.LogWarning(
                "Draft order {OrderId} deletion rejected because stock movements exist.",
                order.Id);
            return ServiceResult.Failure(CreateError(
                ServiceErrorCategory.BusinessRule,
                OrderServiceErrorCodes.DraftHasStockMovements,
                "Stok hareketi bulunan sipariş fiziksel olarak silinemez."));
        }

        dbContext.OrderItems.RemoveRange(order.Items);
        dbContext.Orders.Remove(order);

        await PersistChangesAsync("delete", order, cancellationToken);

        logger.LogInformation("Draft order {OrderId} deleted.", orderId);
        return ServiceResult.Success();
    }

    private Order CreateDraftOrder(
        OrderDraftInputModel input,
        ValidatedDraft validatedDraft,
        string createdByUserId)
    {
        var order = new Order
        {
            OrderNumber = Guid.NewGuid().ToString("N"),
            Status = OrderStatus.Draft,
            OrderDate = timeProvider.GetUtcNow().UtcDateTime,
            TotalAmount = validatedDraft.TotalAmount,
            CreatedByUserId = createdByUserId,
            Items = validatedDraft.Items.Select(CreateOrderItem).ToList()
        };

        ApplyPartySelection(order, input);
        return order;
    }

    private void ApplyDraftUpdate(
        Order order,
        OrderDraftInputModel input,
        ValidatedDraft validatedDraft)
    {
        SynchronizeDraftItems(order, validatedDraft.Items);
        ApplyPartySelection(order, input);
        order.TotalAmount = validatedDraft.TotalAmount;
    }

    // Eşleşen satırların UnitPrice değerine dokunmayarak mevcut sunucu snapshot'ını korur.
    private void SynchronizeDraftItems(
        Order order,
        IReadOnlyList<ValidatedDraftItem> validatedItems)
    {
        var requestedProductIds = validatedItems
            .Select(item => item.ProductId)
            .ToHashSet();
        var removedItems = order.Items
            .Where(item => !requestedProductIds.Contains(item.ProductId))
            .ToList();

        foreach (var removedItem in removedItems)
        {
            order.Items.Remove(removedItem);
            dbContext.OrderItems.Remove(removedItem);
        }

        var existingItems = order.Items.ToDictionary(item => item.ProductId);
        foreach (var validatedItem in validatedItems)
        {
            if (existingItems.TryGetValue(validatedItem.ProductId, out var existingItem))
            {
                existingItem.Quantity = validatedItem.Quantity;
            }
            else
            {
                order.Items.Add(CreateOrderItem(validatedItem));
            }
        }
    }

    private static void ApplyPartySelection(Order order, OrderDraftInputModel input)
    {
        order.Type = input.Type;
        order.CustomerId = input.Type == OrderType.Sale ? input.CustomerId : null;
        order.SupplierId = input.Type == OrderType.Purchase ? input.SupplierId : null;
    }

    private static OrderItem CreateOrderItem(ValidatedDraftItem item)
    {
        return new OrderItem
        {
            ProductId = item.ProductId,
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice
        };
    }

    private static OrderStockConfirmationItem ToStockConfirmationItem(OrderItem item)
    {
        return new OrderStockConfirmationItem(
            item.ProductId,
            item.Quantity,
            item.Product.StockQuantity);
    }

    private ServiceResult<OrderMutationResult> CreateStockPlanningFailure(
        int orderId,
        OrderStockConfirmationFailure failure)
    {
        logger.LogWarning(
            "Order {OrderId} confirmation stock planning rejected with error code {ErrorCode} for product {ProductId}: requested {RequestedQuantity}, available {AvailableQuantity}.",
            orderId,
            failure.ErrorCode,
            failure.ProductId,
            failure.RequestedQuantity,
            failure.AvailableQuantity);

        var message = failure.ErrorCode == OrderServiceErrorCodes.InsufficientStock
            ? "Siparişteki ürünlerden en az biri için yeterli stok bulunmuyor."
            : "Onay işlemi ürün stok miktarını desteklenen aralığın dışına çıkarıyor.";

        return Failure(
            ServiceErrorCategory.BusinessRule,
            failure.ErrorCode,
            message);
    }

    // Yalnız bütün stok satırları planlandıktan sonra tracked entity mutation'ını başlatır.
    private void ApplyConfirmationPlan(
        Order order,
        decimal totalAmount,
        OrderStockConfirmationPlan stockPlan)
    {
        var movementDate = timeProvider.GetUtcNow().UtcDateTime;

        foreach (var item in order.Items)
        {
            item.Product.StockQuantity = stockPlan.NewStockQuantities[item.ProductId];
            dbContext.StockMovements.Add(CreateStockMovement(
                order,
                item,
                stockPlan.MovementType,
                movementDate));
        }

        order.TotalAmount = totalAmount;
        order.Status = OrderStatus.Confirmed;
    }

    private static StockMovement CreateStockMovement(
        Order order,
        OrderItem item,
        StockMovementType movementType,
        DateTime movementDate)
    {
        return new StockMovement
        {
            OrderId = order.Id,
            ProductId = item.ProductId,
            Type = movementType,
            Quantity = item.Quantity,
            Description = BuildMovementDescription(order.OrderNumber, movementType),
            MovementDate = movementDate
        };
    }

    private void LogCommittedStockMovements(Order order, StockMovementType movementType)
    {
        foreach (var item in order.Items)
        {
            logger.LogInformation(
                "Order {OrderId} committed stock movement {MovementType} for product {ProductId}, quantity {Quantity}, new stock {StockQuantity}.",
                order.Id,
                movementType,
                item.ProductId,
                item.Quantity,
                item.Product.StockQuantity);
        }
    }

    private async Task PersistChangesAsync(
        string operation,
        Order order,
        CancellationToken cancellationToken)
    {
        await TrackedPersistence.SaveChangesAsync(
            dbContext,
            exception => logger.LogError(
                exception,
                "Order persistence operation {Operation} failed for order {OrderId} with order number {OrderNumber}.",
                operation,
                order.Id,
                order.OrderNumber),
            cancellationToken);
    }

    private async Task<ServiceResult<ValidatedDraft>> ValidateDraftInputAsync(
        OrderDraftInputModel input,
        IReadOnlyDictionary<int, decimal>? existingPrices,
        CancellationToken cancellationToken)
    {
        var shapeError = ValidateDraftInputShape(input);
        if (shapeError is not null)
        {
            return ServiceResult<ValidatedDraft>.Failure(shapeError);
        }

        var partyValidation = await ValidateInputPartyAsync(input, cancellationToken);
        if (partyValidation is not null)
        {
            return ServiceResult<ValidatedDraft>.Failure(partyValidation);
        }

        var productIds = input.Items.Select(item => item.ProductId).ToList();
        var products = await LoadProductPricesAsync(productIds, cancellationToken);
        if (products.Count != productIds.Count)
        {
            return DraftFailure(
                ServiceErrorCategory.NotFound,
                ProductServiceErrorCodes.ProductNotFound,
                "Sipariş kalemlerinden en az birine ait ürün bulunamadı.");
        }

        return ValidatePricesAndBuildDraft(input.Items, products, existingPrices);
    }

    private static ServiceError? ValidateDraftInputShape(OrderDraftInputModel? input)
    {
        if (input is null)
        {
            return CreateError(
                ServiceErrorCategory.Validation,
                OrderServiceErrorCodes.InputRequired,
                "Sipariş bilgileri zorunludur.");
        }

        if (!Enum.IsDefined(typeof(OrderType), input.Type))
        {
            return CreateError(
                ServiceErrorCategory.Validation,
                OrderServiceErrorCodes.InvalidOrderType,
                "Sipariş türü geçersizdir.");
        }

        if (input.Items is null || input.Items.Count == 0)
        {
            return CreateError(
                ServiceErrorCategory.Validation,
                OrderServiceErrorCodes.ItemsRequired,
                "Sipariş en az bir kalem içermelidir.");
        }

        if (input.Items.Any(item => item.ProductId <= 0))
        {
            return CreateError(
                ServiceErrorCategory.Validation,
                OrderServiceErrorCodes.InvalidProduct,
                "Her sipariş kalemi geçerli bir ürün seçmelidir.");
        }

        if (input.Items.Any(item => item.Quantity <= 0))
        {
            return CreateError(
                ServiceErrorCategory.Validation,
                OrderServiceErrorCodes.InvalidQuantity,
                "Sipariş kalemi miktarı sıfırdan büyük olmalıdır.");
        }

        if (input.Items.Select(item => item.ProductId).Distinct().Count() != input.Items.Count)
        {
            return CreateError(
                ServiceErrorCategory.Validation,
                OrderServiceErrorCodes.DuplicateProduct,
                "Aynı ürün siparişte birden fazla kalem olarak yer alamaz.");
        }

        return null;
    }

    private async Task<IReadOnlyDictionary<int, ProductPrice>> LoadProductPricesAsync(
        IReadOnlyCollection<int> productIds,
        CancellationToken cancellationToken)
    {
        return await dbContext.Products
            .AsNoTracking()
            .Where(product => productIds.Contains(product.Id))
            .Select(product => new ProductPrice(product.Id, product.Price))
            .ToDictionaryAsync(product => product.ProductId, cancellationToken);
    }

    // Mevcut satırda snapshot'ı, yeni satırda güncel ürün fiyatını seçerken toplam sınırını da doğrular.
    private static ServiceResult<ValidatedDraft> ValidatePricesAndBuildDraft(
        IReadOnlyList<OrderItemInputModel> inputItems,
        IReadOnlyDictionary<int, ProductPrice> products,
        IReadOnlyDictionary<int, decimal>? existingPrices)
    {
        var validatedItems = new List<ValidatedDraftItem>(inputItems.Count);
        decimal totalAmount = 0;

        foreach (var inputItem in inputItems)
        {
            var currentPrice = products[inputItem.ProductId].UnitPrice;
            if (currentPrice <= 0)
            {
                return DraftFailure(
                    ServiceErrorCategory.BusinessRule,
                    ProductServiceErrorCodes.PriceInvalid,
                    "Sipariş kalemlerinden en az birinin geçerli bir fiyatı bulunmuyor.");
            }

            var unitPrice = existingPrices is not null
                && existingPrices.TryGetValue(inputItem.ProductId, out var snapshotPrice)
                    ? snapshotPrice
                    : currentPrice;

            if (unitPrice <= 0)
            {
                return DraftFailure(
                    ServiceErrorCategory.BusinessRule,
                    ProductServiceErrorCodes.PriceInvalid,
                    "Sipariş kalemlerinden en az birinin geçerli bir fiyat snapshot'ı bulunmuyor.");
            }

            if (!TryAddLineAmount(totalAmount, inputItem.Quantity, unitPrice, out totalAmount))
            {
                return DraftFailure(
                    ServiceErrorCategory.Validation,
                    OrderServiceErrorCodes.TotalOutOfRange,
                    "Sipariş toplamı desteklenen tutar aralığını aşıyor.");
            }

            validatedItems.Add(new ValidatedDraftItem(
                inputItem.ProductId,
                inputItem.Quantity,
                unitPrice));
        }

        return ServiceResult<ValidatedDraft>.Success(new ValidatedDraft(validatedItems, totalAmount));
    }

    private async Task<ServiceError?> ValidateInputPartyAsync(
        OrderDraftInputModel input,
        CancellationToken cancellationToken)
    {
        if (input.Type == OrderType.Sale)
        {
            if (input.CustomerId is null or <= 0 || input.SupplierId is not null)
            {
                return CreateError(
                    ServiceErrorCategory.Validation,
                    OrderServiceErrorCodes.InvalidParty,
                    "Satış siparişi yalnızca geçerli bir müşteri içermelidir.");
            }

            if (!await dbContext.Customers.AnyAsync(
                    customer => customer.Id == input.CustomerId.Value,
                    cancellationToken))
            {
                return CreateError(
                    ServiceErrorCategory.NotFound,
                    CustomerServiceErrorCodes.CustomerNotFound,
                    "Sipariş için seçilen müşteri bulunamadı.");
            }

            return null;
        }

        if (input.SupplierId is null or <= 0 || input.CustomerId is not null)
        {
            return CreateError(
                ServiceErrorCategory.Validation,
                OrderServiceErrorCodes.InvalidParty,
                "Satın alma siparişi yalnızca geçerli bir tedarikçi içermelidir.");
        }

        if (!await dbContext.Suppliers.AnyAsync(
                supplier => supplier.Id == input.SupplierId.Value,
                cancellationToken))
        {
            return CreateError(
                ServiceErrorCategory.NotFound,
                SupplierServiceErrorCodes.SupplierNotFound,
                "Sipariş için seçilen tedarikçi bulunamadı.");
        }

        return null;
    }

    private async Task<ServiceResult<decimal>> ValidatePersistedDraftAsync(
        Order order,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(typeof(OrderType), order.Type))
        {
            return AmountFailure(
                OrderServiceErrorCodes.InvalidOrderType,
                "Sipariş türü geçersiz olduğu için onaylanamaz.");
        }

        var partyError = await ValidatePersistedPartyAsync(order, cancellationToken);
        if (partyError is not null)
        {
            return ServiceResult<decimal>.Failure(partyError);
        }

        return CalculatePersistedDraftTotal(order.Items);
    }

    private async Task<ServiceError?> ValidatePersistedPartyAsync(
        Order order,
        CancellationToken cancellationToken)
    {
        if (order.Type == OrderType.Sale)
        {
            if (order.CustomerId is null or <= 0 || order.SupplierId is not null)
            {
                return CreateError(
                    ServiceErrorCategory.BusinessRule,
                    OrderServiceErrorCodes.InvalidParty,
                    "Satış siparişinin taraf bilgisi geçersizdir.");
            }

            if (!await dbContext.Customers.AnyAsync(
                    customer => customer.Id == order.CustomerId.Value,
                    cancellationToken))
            {
                return CreateError(
                    ServiceErrorCategory.NotFound,
                    CustomerServiceErrorCodes.CustomerNotFound,
                    "Siparişe ait müşteri bulunamadı.");
            }

            return null;
        }

        if (order.SupplierId is null or <= 0 || order.CustomerId is not null)
        {
            return CreateError(
                ServiceErrorCategory.BusinessRule,
                OrderServiceErrorCodes.InvalidParty,
                "Satın alma siparişinin taraf bilgisi geçersizdir.");
        }

        if (!await dbContext.Suppliers.AnyAsync(
                supplier => supplier.Id == order.SupplierId.Value,
                cancellationToken))
        {
            return CreateError(
                ServiceErrorCategory.NotFound,
                SupplierServiceErrorCodes.SupplierNotFound,
                "Siparişe ait tedarikçi bulunamadı.");
        }

        return null;
    }

    private static ServiceResult<decimal> CalculatePersistedDraftTotal(
        ICollection<OrderItem> items)
    {
        var itemError = ValidatePersistedItems(items);
        if (itemError is not null)
        {
            return ServiceResult<decimal>.Failure(itemError);
        }

        decimal totalAmount = 0;
        foreach (var item in items)
        {
            if (!TryAddLineAmount(totalAmount, item.Quantity, item.UnitPrice, out totalAmount))
            {
                return AmountFailure(
                    OrderServiceErrorCodes.TotalOutOfRange,
                    "Sipariş toplamı desteklenen tutar aralığını aşıyor.");
            }
        }

        return ServiceResult<decimal>.Success(totalAmount);
    }

    private static ServiceError? ValidatePersistedItems(ICollection<OrderItem> items)
    {
        if (items.Count == 0)
        {
            return CreateError(
                ServiceErrorCategory.BusinessRule,
                OrderServiceErrorCodes.ItemsRequired,
                "Kalemi bulunmayan sipariş onaylanamaz.");
        }

        if (items.Select(item => item.ProductId).Distinct().Count() != items.Count)
        {
            return CreateError(
                ServiceErrorCategory.BusinessRule,
                OrderServiceErrorCodes.DuplicateProduct,
                "Aynı ürünü birden fazla kez içeren sipariş onaylanamaz.");
        }

        foreach (var item in items)
        {
            if (item.Product is null)
            {
                return CreateError(
                    ServiceErrorCategory.NotFound,
                    ProductServiceErrorCodes.ProductNotFound,
                    "Sipariş kalemlerinden en az birine ait ürün bulunamadı.");
            }

            if (item.Quantity <= 0)
            {
                return CreateError(
                    ServiceErrorCategory.BusinessRule,
                    OrderServiceErrorCodes.InvalidQuantity,
                    "Geçersiz miktarlı sipariş kalemi onaylanamaz.");
            }

            if (item.UnitPrice <= 0)
            {
                return CreateError(
                    ServiceErrorCategory.BusinessRule,
                    ProductServiceErrorCodes.PriceInvalid,
                    "Geçersiz fiyat snapshot'ı bulunan sipariş onaylanamaz.");
            }
        }

        return null;
    }

    private static bool TryAddLineAmount(
        decimal currentTotal,
        int quantity,
        decimal unitPrice,
        out decimal updatedTotal)
    {
        updatedTotal = currentTotal;

        try
        {
            var lineAmount = checked(unitPrice * quantity);
            var candidateTotal = checked(currentTotal + lineAmount);
            if (lineAmount > MaximumDatabaseAmount || candidateTotal > MaximumDatabaseAmount)
            {
                return false;
            }

            updatedTotal = candidateTotal;
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static string BuildMovementDescription(
        string orderNumber,
        StockMovementType movementType)
    {
        var movementName = movementType == StockMovementType.StockIn
            ? "satın alma stok girişi"
            : "satış stok çıkışı";

        return $"Sipariş {orderNumber}: {movementName}.";
    }

    private static OrderMutationResult ToMutationResult(Order order)
    {
        return new OrderMutationResult(
            order.Id,
            order.OrderNumber,
            order.Status,
            order.TotalAmount);
    }

    private ServiceResult<OrderMutationResult> OrderNotFound()
    {
        return Failure(
            ServiceErrorCategory.NotFound,
            OrderServiceErrorCodes.OrderNotFound,
            "Sipariş bulunamadı.");
    }

    private ServiceResult<OrderMutationResult> OrderNotDraft(Order order, string operation)
    {
        LogStateViolation(order, operation);
        return Failure(
            ServiceErrorCategory.BusinessRule,
            OrderServiceErrorCodes.OrderNotDraft,
            "Bu işlem yalnızca taslak siparişlerde yapılabilir.");
    }

    private void LogStateViolation(Order order, string operation)
    {
        logger.LogWarning(
            "Order {OrderId} operation {Operation} rejected because status is {OrderStatus}.",
            order.Id,
            operation,
            order.Status);
    }

    private static ServiceResult<OrderMutationResult> Failure(
        ServiceErrorCategory category,
        string code,
        string message)
    {
        return ServiceResult<OrderMutationResult>.Failure(CreateError(category, code, message));
    }

    private static ServiceResult<ValidatedDraft> DraftFailure(
        ServiceErrorCategory category,
        string code,
        string message)
    {
        return ServiceResult<ValidatedDraft>.Failure(CreateError(category, code, message));
    }

    private static ServiceResult<decimal> AmountFailure(string code, string message)
    {
        return ServiceResult<decimal>.Failure(CreateError(
            ServiceErrorCategory.BusinessRule,
            code,
            message));
    }

    private static ServiceError CreateError(
        ServiceErrorCategory category,
        string code,
        string message)
    {
        return new ServiceError(category, code, message);
    }

    private async Task<ServiceResult<OrderMutationResult>> RollbackFailureAsync(
        IDbContextTransaction transaction,
        ServiceResult<OrderMutationResult> result,
        CancellationToken cancellationToken)
    {
        await transaction.RollbackAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
        return result;
    }

    // Rollback hatasını loglar ancak asıl confirm exception'ını maskelemez.
    private async Task RollbackAfterUnexpectedFailureAsync(
        IDbContextTransaction transaction,
        int orderId)
    {
        try
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }
        catch (Exception rollbackException)
        {
            logger.LogError(
                rollbackException,
                "Order {OrderId} transaction rollback failed.",
                orderId);
        }
    }

    private sealed record ProductPrice(int ProductId, decimal UnitPrice);

    private sealed record ValidatedDraftItem(int ProductId, int Quantity, decimal UnitPrice);

    private sealed record ValidatedDraft(
        IReadOnlyList<ValidatedDraftItem> Items,
        decimal TotalAmount);

}
