using CustomerService.Api.Contracts;
using CustomerService.Api.Models;
using CustomerService.Api.Mappings;
using CustomerService.Api.Exceptions;

namespace CustomerService.Api.Services;

public class InMemoryCustomerService : ICustomerService
{
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

        EnsureUnique(
            request.Email,
            request.DocumentNumber);

        long id = ++_nextId;

        DateTimeOffset createdAt = DateTimeOffset.UtcNow;
        Customer customer = request.ToModel(id, createdAt);

        _customers.Add(customer);

        return customer;
    }

    public IReadOnlyCollection<Customer> GetAll()
    {
        return _customers.AsReadOnly();
    }

    public Customer? GetById(long id)
    {
        return _customers.FirstOrDefault(
            customer => customer.Id == id);
    }

    public Customer? Update(
        long id,
        UpdateCustomerRequest request)
    {
        Customer? customer = GetById(id);

        if (customer is null)
        {
            return null;
        }

        EnsureUnique(
        request.Email,
        request.DocumentNumber,
        id);

        request.ApplyTo(
            customer,
            DateTimeOffset.UtcNow);

        return customer;
    }

    public bool Delete(long id)
    {
        Customer? customer = GetById(id);

        return customer is not null && _customers.Remove(customer);
    }


    private void EnsureUnique(
        string email,
        string documentNumber,
        long? excludedCustomerId = null)
    {
        bool emailExists = _customers.Any(customer =>
            (!excludedCustomerId.HasValue
                || customer.Id != excludedCustomerId.Value)
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