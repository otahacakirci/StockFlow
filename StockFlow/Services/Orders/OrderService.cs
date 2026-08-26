using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using StockFlow.Data;
using StockFlow.Entities;
using StockFlow.Services.Common;
using StockFlow.ViewModels.Orders;

namespace StockFlow.Services.Orders;

public sealed class OrderService(
    ApplicationDbContext dbContext,
    ILogger<OrderService> logger,
    TimeProvider timeProvider) : IOrderService
{
    private const decimal MaximumDatabaseAmount = 9_999_999_999_999_999.99m;

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
        var order = new Order
        {
            OrderNumber = Guid.NewGuid().ToString("N"),
            Type = input.Type,
            Status = OrderStatus.Draft,
            OrderDate = timeProvider.GetUtcNow().UtcDateTime,
            TotalAmount = validatedDraft.TotalAmount,
            CustomerId = input.Type == OrderType.Sale ? input.CustomerId : null,
            SupplierId = input.Type == OrderType.Purchase ? input.SupplierId : null,
            CreatedByUserId = createdByUserId,
            Items = validatedDraft.Items
                .Select(item => new OrderItem
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice
                })
                .ToList()
        };

        dbContext.Orders.Add(order);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            dbContext.ChangeTracker.Clear();
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Draft order creation failed for order number {OrderNumber}.",
                order.OrderNumber);
            dbContext.ChangeTracker.Clear();
            throw;
        }

        logger.LogInformation(
            "Draft order {OrderId} created with order number {OrderNumber} and {ItemCount} items.",
            order.Id,
            order.OrderNumber,
            order.Items.Count);

        return ServiceResult<OrderMutationResult>.Success(ToMutationResult(order));
    }

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
        var requestedProductIds = validatedDraft.Items
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

        var existingItems = order.Items
            .Where(item => requestedProductIds.Contains(item.ProductId))
            .ToDictionary(item => item.ProductId);

        foreach (var validatedItem in validatedDraft.Items)
        {
            if (existingItems.TryGetValue(validatedItem.ProductId, out var existingItem))
            {
                existingItem.Quantity = validatedItem.Quantity;
                continue;
            }

            order.Items.Add(new OrderItem
            {
                ProductId = validatedItem.ProductId,
                Quantity = validatedItem.Quantity,
                UnitPrice = validatedItem.UnitPrice
            });
        }

        order.Type = input.Type;
        order.CustomerId = input.Type == OrderType.Sale ? input.CustomerId : null;
        order.SupplierId = input.Type == OrderType.Purchase ? input.SupplierId : null;
        order.TotalAmount = validatedDraft.TotalAmount;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            dbContext.ChangeTracker.Clear();
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Draft order {OrderId} update failed.", orderId);
            dbContext.ChangeTracker.Clear();
            throw;
        }

        logger.LogInformation(
            "Draft order {OrderId} updated with {ItemCount} items.",
            order.Id,
            validatedDraft.Items.Count);

        return ServiceResult<OrderMutationResult>.Success(ToMutationResult(order));
    }

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

            var newStockQuantities = new Dictionary<int, int>();

            if (order.Type == OrderType.Sale)
            {
                foreach (var item in order.Items)
                {
                    if (item.Product.StockQuantity < item.Quantity)
                    {
                        logger.LogWarning(
                            "Order {OrderId} confirmation rejected for product {ProductId}: requested {RequestedQuantity}, available {AvailableQuantity}.",
                            order.Id,
                            item.ProductId,
                            item.Quantity,
                            item.Product.StockQuantity);

                        return await RollbackFailureAsync(
                            transaction,
                            Failure(
                                ServiceErrorCategory.BusinessRule,
                                OrderServiceErrorCodes.InsufficientStock,
                                "Siparişteki ürünlerden en az biri için yeterli stok bulunmuyor."),
                            cancellationToken);
                    }

                    newStockQuantities[item.ProductId] = item.Product.StockQuantity - item.Quantity;
                }
            }
            else
            {
                foreach (var item in order.Items)
                {
                    try
                    {
                        newStockQuantities[item.ProductId] = checked(
                            item.Product.StockQuantity + item.Quantity);
                    }
                    catch (OverflowException)
                    {
                        logger.LogWarning(
                            "Order {OrderId} confirmation would overflow stock quantity for product {ProductId}.",
                            order.Id,
                            item.ProductId);

                        return await RollbackFailureAsync(
                            transaction,
                            Failure(
                                ServiceErrorCategory.BusinessRule,
                                OrderServiceErrorCodes.StockQuantityOutOfRange,
                                "Onay işlemi ürün stok miktarını desteklenen aralığın dışına çıkarıyor."),
                            cancellationToken);
                    }
                }
            }

            var movementType = order.Type == OrderType.Purchase
                ? StockMovementType.StockIn
                : StockMovementType.StockOut;
            var movementDate = timeProvider.GetUtcNow().UtcDateTime;

            foreach (var item in order.Items)
            {
                item.Product.StockQuantity = newStockQuantities[item.ProductId];
                dbContext.StockMovements.Add(new StockMovement
                {
                    OrderId = order.Id,
                    ProductId = item.ProductId,
                    Type = movementType,
                    Quantity = item.Quantity,
                    Description = BuildMovementDescription(order.OrderNumber, movementType),
                    MovementDate = movementDate
                });
            }

            order.TotalAmount = persistedValidation.Value;
            order.Status = OrderStatus.Confirmed;

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

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

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            dbContext.ChangeTracker.Clear();
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Draft order {OrderId} cancellation failed.", orderId);
            dbContext.ChangeTracker.Clear();
            throw;
        }

        logger.LogInformation("Draft order {OrderId} cancelled.", order.Id);
        return ServiceResult<OrderMutationResult>.Success(ToMutationResult(order));
    }

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

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            dbContext.ChangeTracker.Clear();
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Draft order {OrderId} deletion failed.", orderId);
            dbContext.ChangeTracker.Clear();
            throw;
        }

        logger.LogInformation("Draft order {OrderId} deleted.", orderId);
        return ServiceResult.Success();
    }

    private async Task<ServiceResult<ValidatedDraft>> ValidateDraftInputAsync(
        OrderDraftInputModel input,
        IReadOnlyDictionary<int, decimal>? existingPrices,
        CancellationToken cancellationToken)
    {
        if (input is null)
        {
            return DraftFailure(
                ServiceErrorCategory.Validation,
                OrderServiceErrorCodes.InputRequired,
                "Sipariş bilgileri zorunludur.");
        }

        if (!Enum.IsDefined(typeof(OrderType), input.Type))
        {
            return DraftFailure(
                ServiceErrorCategory.Validation,
                OrderServiceErrorCodes.InvalidOrderType,
                "Sipariş türü geçersizdir.");
        }

        var partyValidation = await ValidateInputPartyAsync(input, cancellationToken);
        if (partyValidation is not null)
        {
            return ServiceResult<ValidatedDraft>.Failure(partyValidation);
        }

        if (input.Items is null || input.Items.Count == 0)
        {
            return DraftFailure(
                ServiceErrorCategory.Validation,
                OrderServiceErrorCodes.ItemsRequired,
                "Sipariş en az bir kalem içermelidir.");
        }

        if (input.Items.Any(item => item.ProductId <= 0))
        {
            return DraftFailure(
                ServiceErrorCategory.Validation,
                OrderServiceErrorCodes.InvalidProduct,
                "Her sipariş kalemi geçerli bir ürün seçmelidir.");
        }

        if (input.Items.Any(item => item.Quantity <= 0))
        {
            return DraftFailure(
                ServiceErrorCategory.Validation,
                OrderServiceErrorCodes.InvalidQuantity,
                "Sipariş kalemi miktarı sıfırdan büyük olmalıdır.");
        }

        var productIds = input.Items.Select(item => item.ProductId).ToList();
        if (productIds.Distinct().Count() != productIds.Count)
        {
            return DraftFailure(
                ServiceErrorCategory.Validation,
                OrderServiceErrorCodes.DuplicateProduct,
                "Aynı ürün siparişte birden fazla kalem olarak yer alamaz.");
        }

        var products = await dbContext.Products
            .AsNoTracking()
            .Where(product => productIds.Contains(product.Id))
            .Select(product => new ProductPrice(product.Id, product.Price))
            .ToDictionaryAsync(product => product.ProductId, cancellationToken);

        if (products.Count != productIds.Count)
        {
            return DraftFailure(
                ServiceErrorCategory.NotFound,
                OrderServiceErrorCodes.ProductNotFound,
                "Sipariş kalemlerinden en az birine ait ürün bulunamadı.");
        }

        var validatedItems = new List<ValidatedDraftItem>(input.Items.Count);
        decimal totalAmount = 0;

        foreach (var inputItem in input.Items)
        {
            var currentPrice = products[inputItem.ProductId].UnitPrice;
            if (currentPrice <= 0)
            {
                return DraftFailure(
                    ServiceErrorCategory.BusinessRule,
                    OrderServiceErrorCodes.InvalidProductPrice,
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
                    OrderServiceErrorCodes.InvalidProductPrice,
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
                    OrderServiceErrorCodes.CustomerNotFound,
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
                OrderServiceErrorCodes.SupplierNotFound,
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

        if (order.Type == OrderType.Sale)
        {
            if (order.CustomerId is null or <= 0 || order.SupplierId is not null)
            {
                return AmountFailure(
                    OrderServiceErrorCodes.InvalidParty,
                    "Satış siparişinin taraf bilgisi geçersizdir.");
            }

            if (!await dbContext.Customers.AnyAsync(
                    customer => customer.Id == order.CustomerId.Value,
                    cancellationToken))
            {
                return ServiceResult<decimal>.Failure(CreateError(
                    ServiceErrorCategory.NotFound,
                    OrderServiceErrorCodes.CustomerNotFound,
                    "Siparişe ait müşteri bulunamadı."));
            }
        }
        else
        {
            if (order.SupplierId is null or <= 0 || order.CustomerId is not null)
            {
                return AmountFailure(
                    OrderServiceErrorCodes.InvalidParty,
                    "Satın alma siparişinin taraf bilgisi geçersizdir.");
            }

            if (!await dbContext.Suppliers.AnyAsync(
                    supplier => supplier.Id == order.SupplierId.Value,
                    cancellationToken))
            {
                return ServiceResult<decimal>.Failure(CreateError(
                    ServiceErrorCategory.NotFound,
                    OrderServiceErrorCodes.SupplierNotFound,
                    "Siparişe ait tedarikçi bulunamadı."));
            }
        }

        if (order.Items.Count == 0)
        {
            return AmountFailure(
                OrderServiceErrorCodes.ItemsRequired,
                "Kalemi bulunmayan sipariş onaylanamaz.");
        }

        if (order.Items.Select(item => item.ProductId).Distinct().Count() != order.Items.Count)
        {
            return AmountFailure(
                OrderServiceErrorCodes.DuplicateProduct,
                "Aynı ürünü birden fazla kez içeren sipariş onaylanamaz.");
        }

        decimal totalAmount = 0;
        foreach (var item in order.Items)
        {
            if (item.Product is null)
            {
                return ServiceResult<decimal>.Failure(CreateError(
                    ServiceErrorCategory.NotFound,
                    OrderServiceErrorCodes.ProductNotFound,
                    "Sipariş kalemlerinden en az birine ait ürün bulunamadı."));
            }

            if (item.Quantity <= 0)
            {
                return AmountFailure(
                    OrderServiceErrorCodes.InvalidQuantity,
                    "Geçersiz miktarlı sipariş kalemi onaylanamaz.");
            }

            if (item.UnitPrice <= 0)
            {
                return AmountFailure(
                    OrderServiceErrorCodes.InvalidProductPrice,
                    "Geçersiz fiyat snapshot'ı bulunan sipariş onaylanamaz.");
            }

            if (!TryAddLineAmount(totalAmount, item.Quantity, item.UnitPrice, out totalAmount))
            {
                return AmountFailure(
                    OrderServiceErrorCodes.TotalOutOfRange,
                    "Sipariş toplamı desteklenen tutar aralığını aşıyor.");
            }
        }

        return ServiceResult<decimal>.Success(totalAmount);
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
