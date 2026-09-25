using CustomerService.Api.Models;
using CustomerService.Api.Contracts;

namespace CustomerService.Api.Services;
                                                         
public interface ICustomerService
{
    IReadOnlyCollection<Customer> GetAll();

    Customer? GetById(long id);

    Customer Create(CreateCustomerRequest request);

    Customer? Update(long id, UpdateCustomerRequest request);

    bool Delete(long id);
}