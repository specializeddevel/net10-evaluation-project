using CustomerService.Api.Contracts;
using CustomerService.Api.Models;

namespace CustomerService.Api.Services;

public interface ICustomerService
{
    IReadOnlyCollection<Customer> GetAll();

    (IReadOnlyCollection<Customer> Items, int TotalCount) Search(
    string? search,
    int page,
    int pageSize,
    bool sortDescending);

    Task<(IReadOnlyCollection<Customer> Items, int TotalCount)> SearchAsync(
    string? search,
    int page,
    int pageSize,
    bool sortDescending,
    CancellationToken cancellationToken);

    Customer? GetById(long id);

    Task<Customer?> GetByIdAsync(
    long id,
    CancellationToken cancellationToken);

    Customer Create(CreateCustomerRequest request);

    Task<Customer> CreateAsync(
        CreateCustomerRequest request,
        CancellationToken cancellationToken);

    Customer? Update(long id, UpdateCustomerRequest request);


    Task<Customer?> UpdateAsync(
        long id,
        UpdateCustomerRequest request,
        CancellationToken cancellationToken);

    bool Delete(long id);

    Task<bool> DeleteAsync(
    long id,
    CancellationToken cancellationToken);

}