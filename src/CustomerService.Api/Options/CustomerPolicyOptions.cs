using System.ComponentModel.DataAnnotations;

namespace CustomerService.Api.Options;

public sealed class CustomerPolicyOptions
{
    public const string SectionName = "CustomerPolicy";

    [Range(
        1,
        120,
        ErrorMessage = "MinimumAge must be between 1 and 120.")]
    public int MinimumAge { get; set; }
}