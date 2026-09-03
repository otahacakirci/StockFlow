using Microsoft.EntityFrameworkCore;
using StockFlow.Data;
using StockFlow.Entities;
using StockFlow.Services.Common;
using StockFlow.ViewModels.Suppliers;

namespace StockFlow.Services.Suppliers;

/// <summary>
/// Supplier sorgularını, iletişim alanı doğrulamasını ve sipariş geçmişi korumalı kalıcılaştırma akışını yönetir.
/// </summary>
internal sealed class SupplierService(
    ApplicationDbContext dbContext,
    ILogger<SupplierService> logger) : ISupplierService
{
    private const int MaximumCompanyNameLength = 200;
    private static readonly ContactInformationErrorCodes ContactErrorCodes = new(
        SupplierServiceErrorCodes.EmailTooLong,
        SupplierServiceErrorCodes.EmailInvalid,
        SupplierServiceErrorCodes.PhoneTooLong,
        SupplierServiceErrorCodes.PhoneInvalid,
        SupplierServiceErrorCodes.AddressTooLong);

    public async Task<ServiceResult<SupplierListViewModel>> GetListAsync(
        SupplierListQueryModel? query = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuery = NormalizeQuery(query);
        var suppliers = dbContext.Suppliers.AsNoTracking();

        if (normalizedQuery.SearchTerm is not null)
        {
            suppliers = suppliers.Where(supplier =>
                supplier.CompanyName.Contains(normalizedQuery.SearchTerm)
                || (supplier.Email != null && supplier.Email.Contains(normalizedQuery.SearchTerm))
                || (supplier.Phone != null && supplier.Phone.Contains(normalizedQuery.SearchTerm)));
        }

        var totalCount = await suppliers.CountAsync(cancellationToken);
        var page = ListPagingPolicy.Resolve(normalizedQuery.PageRequest, totalCount);

        var orderedSuppliers = normalizedQuery.SortOrder == SupplierSortOrder.CompanyNameDescending
            ? suppliers.OrderByDescending(supplier => supplier.CompanyName).ThenByDescending(supplier => supplier.Id)
            : suppliers.OrderBy(supplier => supplier.CompanyName).ThenBy(supplier => supplier.Id);

        var items = await orderedSuppliers
            .Skip(page.Offset)
            .Take(page.PageSize)
            .Select(supplier => new SupplierViewModel(
                supplier.Id,
                supplier.CompanyName,
                supplier.Email,
                supplier.Phone,
                supplier.Address,
                supplier.Orders.Count))
            .ToListAsync(cancellationToken);

        return ServiceResult<SupplierListViewModel>.Success(new SupplierListViewModel(
            items,
            normalizedQuery.SearchTerm,
            normalizedQuery.SortOrder,
            page.Page,
            page.PageSize,
            totalCount,
            page.TotalPages));
    }

    public async Task<ServiceResult<SupplierViewModel>> GetByIdAsync(
        int supplierId,
        CancellationToken cancellationToken = default)
    {
        var supplier = await dbContext.Suppliers
            .AsNoTracking()
            .Where(candidate => candidate.Id == supplierId)
            .Select(candidate => new SupplierViewModel(
                candidate.Id,
                candidate.CompanyName,
                candidate.Email,
                candidate.Phone,
                candidate.Address,
                candidate.Orders.Count))
            .SingleOrDefaultAsync(cancellationToken);

        return supplier is null
            ? SupplierNotFound<SupplierViewModel>()
            : ServiceResult<SupplierViewModel>.Success(supplier);
    }

    public async Task<ServiceResult<IReadOnlyList<SupplierSelectionOptionViewModel>>> GetSelectionOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        var options = await dbContext.Suppliers
            .AsNoTracking()
            .OrderBy(supplier => supplier.CompanyName)
            .ThenBy(supplier => supplier.Id)
            .Select(supplier => new SupplierSelectionOptionViewModel(
                supplier.Id,
                supplier.CompanyName))
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<SupplierSelectionOptionViewModel>>.Success(options);
    }

    public async Task<ServiceResult<SupplierViewModel>> CreateAsync(
        SupplierInputModel? input,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateAndNormalizeInput(input, out var validatedInput);
        if (validationError is not null)
        {
            return Rejected<SupplierViewModel>("create", validationError);
        }

        var supplier = new Supplier
        {
            CompanyName = validatedInput.CompanyName,
            Email = validatedInput.Email,
            Phone = validatedInput.Phone,
            Address = validatedInput.Address
        };

        dbContext.Suppliers.Add(supplier);
        await PersistChangesAsync("create", supplier, cancellationToken);

        logger.LogInformation("Supplier {SupplierId} created.", supplier.Id);
        return ServiceResult<SupplierViewModel>.Success(ToViewModel(supplier, orderCount: 0));
    }

    public async Task<ServiceResult<SupplierViewModel>> UpdateAsync(
        int supplierId,
        SupplierInputModel? input,
        CancellationToken cancellationToken = default)
    {
        var supplier = await dbContext.Suppliers
            .SingleOrDefaultAsync(candidate => candidate.Id == supplierId, cancellationToken);
        if (supplier is null)
        {
            return SupplierNotFound<SupplierViewModel>();
        }

        var validationError = ValidateAndNormalizeInput(input, out var validatedInput);
        if (validationError is not null)
        {
            return Rejected<SupplierViewModel>("update", validationError, supplier.Id);
        }

        var orderCount = await dbContext.Orders.CountAsync(
            order => order.SupplierId == supplier.Id,
            cancellationToken);

        supplier.CompanyName = validatedInput.CompanyName;
        supplier.Email = validatedInput.Email;
        supplier.Phone = validatedInput.Phone;
        supplier.Address = validatedInput.Address;

        await PersistChangesAsync("update", supplier, cancellationToken);

        logger.LogInformation("Supplier {SupplierId} updated.", supplier.Id);
        return ServiceResult<SupplierViewModel>.Success(ToViewModel(supplier, orderCount));
    }

    public async Task<ServiceResult> DeleteAsync(
        int supplierId,
        CancellationToken cancellationToken = default)
    {
        var supplier = await dbContext.Suppliers
            .SingleOrDefaultAsync(candidate => candidate.Id == supplierId, cancellationToken);
        if (supplier is null)
        {
            return ServiceResult.Failure(SupplierNotFoundError());
        }

        if (await dbContext.Orders.AnyAsync(
                order => order.SupplierId == supplier.Id,
                cancellationToken))
        {
            logger.LogWarning(
                "Supplier {SupplierId} deletion rejected because order history exists.",
                supplier.Id);
            return ServiceResult.Failure(CreateError(
                ServiceErrorCategory.BusinessRule,
                SupplierServiceErrorCodes.SupplierHasOrders,
                "Sipariş geçmişi bulunan tedarikçi fiziksel olarak silinemez."));
        }

        dbContext.Suppliers.Remove(supplier);
        await PersistChangesAsync("delete", supplier, cancellationToken);

        logger.LogInformation("Supplier {SupplierId} deleted.", supplierId);
        return ServiceResult.Success();
    }

    private async Task PersistChangesAsync(
        string operation,
        Supplier supplier,
        CancellationToken cancellationToken)
    {
        await TrackedPersistence.SaveChangesAsync(
            dbContext,
            exception => logger.LogError(
                exception,
                "Supplier persistence operation {Operation} failed for supplier {SupplierId}.",
                operation,
                supplier.Id),
            cancellationToken);
    }

    private static NormalizedSupplierListQuery NormalizeQuery(SupplierListQueryModel? query)
    {
        var searchTerm = string.IsNullOrWhiteSpace(query?.SearchTerm)
            ? null
            : query.SearchTerm.Trim();
        var sortOrder = query is not null && Enum.IsDefined(query.SortOrder)
            ? query.SortOrder
            : SupplierSortOrder.CompanyNameAscending;
        var pageRequest = ListPagingPolicy.Normalize(query?.Page, query?.PageSize);

        return new NormalizedSupplierListQuery(
            searchTerm,
            sortOrder,
            pageRequest);
    }

    private static ServiceError? ValidateAndNormalizeInput(
        SupplierInputModel? input,
        out ValidatedSupplierInput validatedInput)
    {
        validatedInput = ValidatedSupplierInput.Empty;

        if (input is null)
        {
            return CreateError(
                ServiceErrorCategory.Validation,
                SupplierServiceErrorCodes.InputRequired,
                "Tedarikçi bilgileri zorunludur.");
        }

        var companyName = input.CompanyName?.Trim() ?? string.Empty;

        if (companyName.Length == 0)
        {
            return CreateError(
                ServiceErrorCategory.Validation,
                SupplierServiceErrorCodes.CompanyNameRequired,
                "Şirket adı zorunludur.");
        }

        if (companyName.Length > MaximumCompanyNameLength)
        {
            return CreateError(
                ServiceErrorCategory.Validation,
                SupplierServiceErrorCodes.CompanyNameTooLong,
                $"Şirket adı en fazla {MaximumCompanyNameLength} karakter olabilir.");
        }

        var contactResult = ContactInformationPolicy.ValidateAndNormalize(
            input.Email,
            input.Phone,
            input.Address,
            ContactErrorCodes);
        if (!contactResult.IsSuccess)
        {
            return contactResult.Error;
        }

        var contact = contactResult.Value!;
        validatedInput = new ValidatedSupplierInput(
            companyName,
            contact.Email,
            contact.Phone,
            contact.Address);

        return null;
    }

    private ServiceResult<T> Rejected<T>(
        string operation,
        ServiceError error,
        int supplierId = 0)
    {
        logger.LogWarning(
            "Supplier operation {Operation} rejected with error code {ErrorCode} for supplier {SupplierId}.",
            operation,
            error.Code,
            supplierId);
        return ServiceResult<T>.Failure(error);
    }

    private static SupplierViewModel ToViewModel(Supplier supplier, int orderCount)
    {
        return new SupplierViewModel(
            supplier.Id,
            supplier.CompanyName,
            supplier.Email,
            supplier.Phone,
            supplier.Address,
            orderCount);
    }

    private static ServiceResult<T> SupplierNotFound<T>()
    {
        return ServiceResult<T>.Failure(SupplierNotFoundError());
    }

    private static ServiceError SupplierNotFoundError()
    {
        return CreateError(
            ServiceErrorCategory.NotFound,
            SupplierServiceErrorCodes.SupplierNotFound,
            "Tedarikçi bulunamadı.");
    }

    private static ServiceError CreateError(
        ServiceErrorCategory category,
        string code,
        string message)
    {
        return new ServiceError(category, code, message);
    }

    private sealed record NormalizedSupplierListQuery(
        string? SearchTerm,
        SupplierSortOrder SortOrder,
        NormalizedPageRequest PageRequest);

    private sealed record ValidatedSupplierInput(
        string CompanyName,
        string? Email,
        string? Phone,
        string? Address)
    {
        public static ValidatedSupplierInput Empty { get; } = new(string.Empty, null, null, null);
    }
}
