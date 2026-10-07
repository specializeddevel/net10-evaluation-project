using System.ComponentModel.DataAnnotations;

namespace CustomerService.Api.Contracts;

public sealed record GetCustomersRequest
{
    [StringLength(100)]
    public string? Search { get; init; }

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 10;

    public bool SortDescending { get; init; }
}