using CustomerService.Api.Contracts;
using CustomerService.Api.Models;

namespace CustomerService.Api.Mappings;

public static class CustomerMappings
{
    public static CustomerResponse ToResponse(this Customer customer)
    {
        return new CustomerResponse
        {
            Id = customer.Id,
            FirstName = customer.FirstName,
            LastName = customer.LastName,
            Email = customer.Email,
            DocumentNumber = customer.DocumentNumber,
            BirthDate = customer.BirthDate,
            CreatedAt = customer.CreatedAt,
            UpdatedAt = customer.UpdatedAt
        };
    }

    public static Customer ToModel(
        this CreateCustomerRequest request,
        long id,
        DateTimeOffset createdAt)
    {
        return new Customer
        {
            Id = id,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            DocumentNumber = request.DocumentNumber,
            BirthDate = request.BirthDate,
            CreatedAt = createdAt,
            UpdatedAt = null
        };
    }

    public static void ApplyTo(
        this UpdateCustomerRequest request,
        Customer customer,
        DateTimeOffset updatedAt)
    {
        customer.FirstName = request.FirstName;
        customer.LastName = request.LastName;
        customer.Email = request.Email;
        customer.DocumentNumber = request.DocumentNumber;
        customer.BirthDate = request.BirthDate;
        customer.UpdatedAt = updatedAt;
    }
}