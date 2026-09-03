using Microsoft.EntityFrameworkCore;
using StockFlow.Data;
using StockFlow.Entities;
using StockFlow.Services.Common;
using StockFlow.ViewModels.Customers;

namespace StockFlow.Services.Customers;

/// <summary>
/// Customer sorgularını, iletişim alanı doğrulamasını ve sipariş geçmişi korumalı kalıcılaştırma akışını yönetir.
/// </summary>
internal sealed class CustomerService(
    ApplicationDbContext dbContext,
    ILogger<CustomerService> logger) : ICustomerService
{
    private const int MaximumNameLength = 150;
    private static readonly ContactInformationErrorCodes ContactErrorCodes = new(
        CustomerServiceErrorCodes.EmailTooLong,
        CustomerServiceErrorCodes.EmailInvalid,
        CustomerServiceErrorCodes.PhoneTooLong,
        CustomerServiceErrorCodes.PhoneInvalid,
        CustomerServiceErrorCodes.AddressTooLong);

    public async Task<ServiceResult<CustomerListViewModel>> GetListAsync(
        CustomerListQueryModel? query = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuery = NormalizeQuery(query);
        var customers = dbContext.Customers.AsNoTracking();

        if (normalizedQuery.SearchTerm is not null)
        {
            customers = customers.Where(customer =>
                customer.Name.Contains(normalizedQuery.SearchTerm)
                || (customer.Email != null && customer.Email.Contains(normalizedQuery.SearchTerm))
                || (customer.Phone != null && customer.Phone.Contains(normalizedQuery.SearchTerm)));
        }

        var totalCount = await customers.CountAsync(cancellationToken);
        var page = ListPagingPolicy.Resolve(normalizedQuery.PageRequest, totalCount);

        var orderedCustomers = normalizedQuery.SortOrder == CustomerSortOrder.NameDescending
            ? customers.OrderByDescending(customer => customer.Name).ThenByDescending(customer => customer.Id)
            : customers.OrderBy(customer => customer.Name).ThenBy(customer => customer.Id);

        var items = await orderedCustomers
            .Skip(page.Offset)
            .Take(page.PageSize)
            .Select(customer => new CustomerViewModel(
                customer.Id,
                customer.Name,
                customer.Email,
                customer.Phone,
                customer.Address,
                customer.Orders.Count))
            .ToListAsync(cancellationToken);

        return ServiceResult<CustomerListViewModel>.Success(new CustomerListViewModel(
            items,
            normalizedQuery.SearchTerm,
            normalizedQuery.SortOrder,
            page.Page,
            page.PageSize,
            totalCount,
            page.TotalPages));
    }

    public async Task<ServiceResult<CustomerViewModel>> GetByIdAsync(
        int customerId,
        CancellationToken cancellationToken = default)
    {
        var customer = await dbContext.Customers
            .AsNoTracking()
            .Where(candidate => candidate.Id == customerId)
            .Select(candidate => new CustomerViewModel(
                candidate.Id,
                candidate.Name,
                candidate.Email,
                candidate.Phone,
                candidate.Address,
                candidate.Orders.Count))
            .SingleOrDefaultAsync(cancellationToken);

        return customer is null
            ? CustomerNotFound<CustomerViewModel>()
            : ServiceResult<CustomerViewModel>.Success(customer);
    }

    public async Task<ServiceResult<IReadOnlyList<CustomerSelectionOptionViewModel>>> GetSelectionOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        var options = await dbContext.Customers
            .AsNoTracking()
            .OrderBy(customer => customer.Name)
            .ThenBy(customer => customer.Id)
            .Select(customer => new CustomerSelectionOptionViewModel(
                customer.Id,
                customer.Name))
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<CustomerSelectionOptionViewModel>>.Success(options);
    }

    public async Task<ServiceResult<CustomerViewModel>> CreateAsync(
        CustomerInputModel? input,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateAndNormalizeInput(input, out var validatedInput);
        if (validationError is not null)
        {
            return Rejected<CustomerViewModel>("create", validationError);
        }

        var customer = new Customer
        {
            Name = validatedInput.Name,
            Email = validatedInput.Email,
            Phone = validatedInput.Phone,
            Address = validatedInput.Address
        };

        dbContext.Customers.Add(customer);
        await PersistChangesAsync("create", customer, cancellationToken);

        logger.LogInformation("Customer {CustomerId} created.", customer.Id);
        return ServiceResult<CustomerViewModel>.Success(ToViewModel(customer, orderCount: 0));
    }

    public async Task<ServiceResult<CustomerViewModel>> UpdateAsync(
        int customerId,
        CustomerInputModel? input,
        CancellationToken cancellationToken = default)
    {
        var customer = await dbContext.Customers
            .SingleOrDefaultAsync(candidate => candidate.Id == customerId, cancellationToken);
        if (customer is null)
        {
            return CustomerNotFound<CustomerViewModel>();
        }

        var validationError = ValidateAndNormalizeInput(input, out var validatedInput);
        if (validationError is not null)
        {
            return Rejected<CustomerViewModel>("update", validationError, customer.Id);
        }

        var orderCount = await dbContext.Orders.CountAsync(
            order => order.CustomerId == customer.Id,
            cancellationToken);

        customer.Name = validatedInput.Name;
        customer.Email = validatedInput.Email;
        customer.Phone = validatedInput.Phone;
        customer.Address = validatedInput.Address;

        await PersistChangesAsync("update", customer, cancellationToken);

        logger.LogInformation("Customer {CustomerId} updated.", customer.Id);
        return ServiceResult<CustomerViewModel>.Success(ToViewModel(customer, orderCount));
    }

    public async Task<ServiceResult> DeleteAsync(
        int customerId,
        CancellationToken cancellationToken = default)
    {
        var customer = await dbContext.Customers
            .SingleOrDefaultAsync(candidate => candidate.Id == customerId, cancellationToken);
        if (customer is null)
        {
            return ServiceResult.Failure(CustomerNotFoundError());
        }

        if (await dbContext.Orders.AnyAsync(
                order => order.CustomerId == customer.Id,
                cancellationToken))
        {
            logger.LogWarning(
                "Customer {CustomerId} deletion rejected because order history exists.",
                customer.Id);
            return ServiceResult.Failure(CreateError(
                ServiceErrorCategory.BusinessRule,
                CustomerServiceErrorCodes.CustomerHasOrders,
                "Sipariş geçmişi bulunan müşteri fiziksel olarak silinemez."));
        }

        dbContext.Customers.Remove(customer);
        await PersistChangesAsync("delete", customer, cancellationToken);

        logger.LogInformation("Customer {CustomerId} deleted.", customerId);
        return ServiceResult.Success();
    }

    private async Task PersistChangesAsync(
        string operation,
        Customer customer,
        CancellationToken cancellationToken)
    {
        await TrackedPersistence.SaveChangesAsync(
            dbContext,
            exception => logger.LogError(
                exception,
                "Customer persistence operation {Operation} failed for customer {CustomerId}.",
                operation,
                customer.Id),
            cancellationToken);
    }

    private static NormalizedCustomerListQuery NormalizeQuery(CustomerListQueryModel? query)
    {
        var searchTerm = string.IsNullOrWhiteSpace(query?.SearchTerm)
            ? null
            : query.SearchTerm.Trim();
        var sortOrder = query is not null && Enum.IsDefined(query.SortOrder)
            ? query.SortOrder
            : CustomerSortOrder.NameAscending;
        var pageRequest = ListPagingPolicy.Normalize(query?.Page, query?.PageSize);

        return new NormalizedCustomerListQuery(
            searchTerm,
            sortOrder,
            pageRequest);
    }

    private static ServiceError? ValidateAndNormalizeInput(
        CustomerInputModel? input,
        out ValidatedCustomerInput validatedInput)
    {
        validatedInput = ValidatedCustomerInput.Empty;

        if (input is null)
        {
            return CreateError(
                ServiceErrorCategory.Validation,
                CustomerServiceErrorCodes.InputRequired,
                "Müşteri bilgileri zorunludur.");
        }

        var name = input.Name?.Trim() ?? string.Empty;

        if (name.Length == 0)
        {
            return CreateError(
                ServiceErrorCategory.Validation,
                CustomerServiceErrorCodes.NameRequired,
                "Müşteri adı zorunludur.");
        }

        if (name.Length > MaximumNameLength)
        {
            return CreateError(
                ServiceErrorCategory.Validation,
                CustomerServiceErrorCodes.NameTooLong,
                $"Müşteri adı en fazla {MaximumNameLength} karakter olabilir.");
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
        validatedInput = new ValidatedCustomerInput(
            name,
            contact.Email,
            contact.Phone,
            contact.Address);

        return null;
    }

    private ServiceResult<T> Rejected<T>(
        string operation,
        ServiceError error,
        int customerId = 0)
    {
        logger.LogWarning(
            "Customer operation {Operation} rejected with error code {ErrorCode} for customer {CustomerId}.",
            operation,
            error.Code,
            customerId);
        return ServiceResult<T>.Failure(error);
    }

    private static CustomerViewModel ToViewModel(Customer customer, int orderCount)
    {
        return new CustomerViewModel(
            customer.Id,
            customer.Name,
            customer.Email,
            customer.Phone,
            customer.Address,
            orderCount);
    }

    private static ServiceResult<T> CustomerNotFound<T>()
    {
        return ServiceResult<T>.Failure(CustomerNotFoundError());
    }

    private static ServiceError CustomerNotFoundError()
    {
        return CreateError(
            ServiceErrorCategory.NotFound,
            CustomerServiceErrorCodes.CustomerNotFound,
            "Müşteri bulunamadı.");
    }

    private static ServiceError CreateError(
        ServiceErrorCategory category,
        string code,
        string message)
    {
        return new ServiceError(category, code, message);
    }

    private sealed record NormalizedCustomerListQuery(
        string? SearchTerm,
        CustomerSortOrder SortOrder,
        NormalizedPageRequest PageRequest);

    private sealed record ValidatedCustomerInput(
        string Name,
        string? Email,
        string? Phone,
        string? Address)
    {
        public static ValidatedCustomerInput Empty { get; } = new(string.Empty, null, null, null);
    }
}
