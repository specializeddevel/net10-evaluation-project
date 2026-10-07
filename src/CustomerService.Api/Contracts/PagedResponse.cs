namespace CustomerService.Api.Contracts;

public sealed record PagedResponse<T>(
    IReadOnlyCollection<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages =>
        (int)Math.Ceiling((double)TotalCount / PageSize);
}