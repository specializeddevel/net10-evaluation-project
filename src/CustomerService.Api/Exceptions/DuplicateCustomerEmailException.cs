namespace CustomerService.Api.Exceptions;


public sealed class DuplicateCustomerEmailException : Exception
{
    public DuplicateCustomerEmailException()
        : base("A customer with the same email already exists.")
    {
    }
}