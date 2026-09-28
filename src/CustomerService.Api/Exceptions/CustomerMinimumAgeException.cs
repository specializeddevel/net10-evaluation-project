namespace CustomerService.Api.Exceptions;

public sealed class CustomerMinimumAgeException : Exception
{
    public CustomerMinimumAgeException(int minimumAge)
        : base(
            $"Customer must be at least {minimumAge} years old.")
    {
        MinimumAge = minimumAge;
    }

    public int MinimumAge { get; }
}