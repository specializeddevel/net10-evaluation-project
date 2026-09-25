namespace CustomerService.Api.Exceptions;


public sealed class DuplicateCustomerDocumentException : Exception
{
    public DuplicateCustomerDocumentException()
        : base("A customer with the same document number already exists.")
    {
    }
}