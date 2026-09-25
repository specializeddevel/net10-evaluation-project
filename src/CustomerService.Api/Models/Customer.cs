namespace CustomerService.Api.Models;

public sealed class Customer
{
    public long Id { get; init; }

    public required string FirstName { get; set; }

    public required string LastName { get; set; }

    public required string Email { get; set; }

    public required string DocumentNumber { get; set; }

    public DateOnly BirthDate { get; set; }

    public DateTimeOffset CreatedAt { get; init; } //= DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }
}