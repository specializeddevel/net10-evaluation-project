namespace CustomerService.Api.Contracts;

public sealed record CustomerResponse
{
    public required long Id { get; init; }

    public required string FirstName { get; init; }

    public required string LastName { get; init; }

    public required string Email { get; init; }

    public required string DocumentNumber { get; init; }

    public required DateOnly BirthDate { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }
}