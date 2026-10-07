using CustomerService.Api.Contracts;
using CustomerService.Api.Exceptions;
using CustomerService.Api.Mappings;
using CustomerService.Api.Models;
using CustomerService.Api.Options;
using Microsoft.Extensions.Options;

namespace CustomerService.Api.Services;

public class InMemoryCustomerService : ICustomerService
{
    private readonly CustomerPolicyOptions _customerPolicyOptions;

    public InMemoryCustomerService(
        IOptions<CustomerPolicyOptions> customerPolicyOptions)
    {
        _customerPolicyOptions = customerPolicyOptions.Value;
    }

    private readonly List<Customer> _customers =
    [
        new Customer
        {
            Id = 1,
            FirstName = "Raúl",
            LastName = "Burgos",
            Email = "raul@example.com",
            DocumentNumber = "12345678",
            BirthDate = new DateOnly(Random.Shared.Next(1980, 2000), Random.Shared.Next(1, 12), Random.Shared.Next(1, 28))
        },
        new Customer
        {
            Id = 2,
            FirstName = "Ana",
            LastName = "García",
            Email = "ana@example.com",
            DocumentNumber = "87654321",
            BirthDate = new DateOnly(1990, 6, 20)
        }
    ];

    private long _nextId = 2;

    public Customer Create(CreateCustomerRequest request)
    {

        EnsureMinimumAge(request.BirthDate);

        EnsureUnique(
            request.Email,
            request.DocumentNumber);

        long id = ++_nextId;

        DateTimeOffset createdAt = DateTimeOffset.UtcNow;
        Customer customer = request.ToModel(id, createdAt);

        _customers.Add(customer);

        return customer;
    }

    public Task<Customer> CreateAsync(
        CreateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Customer customer = Create(request);

        return Task.FromResult(customer);
    }

    public IReadOnlyCollection<Customer> GetAll()
    {
        return _customers.AsReadOnly();
    }

    public (IReadOnlyCollection<Customer> Items, int TotalCount) Search(
        string? search,
        int page,
        int pageSize,
        bool sortDescending)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 100);

        IEnumerable<Customer> query = _customers;

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();

            query = query.Where(customer =>
                customer.FirstName.Contains(
                    term, StringComparison.OrdinalIgnoreCase)
                || customer.LastName.Contains(
                    term, StringComparison.OrdinalIgnoreCase)
                || customer.Email.Contains(
                    term, StringComparison.OrdinalIgnoreCase));
        }

        int totalCount = query.Count();

        IOrderedEnumerable<Customer> orderedQuery = sortDescending
            ? query
                .OrderByDescending(customer => customer.LastName)
                .ThenByDescending(customer => customer.FirstName)
                .ThenByDescending(customer => customer.Id)
            : query
                .OrderBy(customer => customer.LastName)
                .ThenBy(customer => customer.FirstName)
                .ThenBy(customer => customer.Id);

        long offset = ((long)page - 1) * pageSize;

        Customer[] items = offset >= totalCount
            ? []
            : orderedQuery
                .Skip((int)offset)
                .Take(pageSize)
                .ToArray();

        return (items, totalCount);
    }

    public Task<(IReadOnlyCollection<Customer> Items, int TotalCount)> SearchAsync(
    string? search,
    int page,
    int pageSize,
    bool sortDescending,
    CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = Search(
            search,
            page,
            pageSize,
            sortDescending);

        return Task.FromResult(result);
    }

    public Customer? GetById(long id)
    {
        return _customers.FirstOrDefault(
            customer => customer.Id == id);
    }

    public Task<Customer?> GetByIdAsync(
    long id,
    CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Customer? customer = GetById(id);

        return Task.FromResult<Customer?>(customer);
    }


    public Customer? Update(
        long id,
        UpdateCustomerRequest request)
    {

        if (GetById(id) is not Customer customer)
        {
            return null;
        }

        EnsureMinimumAge(request.BirthDate);

        EnsureUnique(
        request.Email,
        request.DocumentNumber,
        id);

        request.ApplyTo(
            customer,
            DateTimeOffset.UtcNow);

        return customer;
    }


    public Task<Customer?> UpdateAsync(
        long id,
        UpdateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Customer? customer = Update(id, request);

        return Task.FromResult<Customer?>(customer);
    }

    public bool Delete(long id)
    {
        Customer? customer = GetById(id);

        return customer is not null && _customers.Remove(customer);
    }

    public Task<bool> DeleteAsync(
        long id,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        bool deleted = Delete(id);

        return Task.FromResult(deleted);
    }

    private void EnsureMinimumAge(DateOnly birthDate)
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);

        DateOnly maximumAllowedBirthDate =
            today.AddYears(-_customerPolicyOptions.MinimumAge);

        if (birthDate > maximumAllowedBirthDate)
        {
            throw new CustomerMinimumAgeException(
                _customerPolicyOptions.MinimumAge);
        }
    }

    private void EnsureUnique(
        string email,
        string documentNumber,
        long? excludedCustomerId = null)
    {
        bool emailExists = _customers.Any(customer =>
            (excludedCustomerId is not long idToExclude
            || customer.Id != idToExclude)

            && string.Equals(
                customer.Email.Trim(),
                email.Trim(),
                StringComparison.OrdinalIgnoreCase));

        if (emailExists)
        {
            throw new DuplicateCustomerEmailException();
        }

        bool documentExists = _customers.Any(customer =>
            (!excludedCustomerId.HasValue
                || customer.Id != excludedCustomerId.Value)
            && string.Equals(
                customer.DocumentNumber.Trim(),
                documentNumber.Trim(),
                StringComparison.OrdinalIgnoreCase));

        if (documentExists)
        {
            throw new DuplicateCustomerDocumentException();
        }
    }

}