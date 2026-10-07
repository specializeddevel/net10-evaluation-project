using System.ComponentModel.DataAnnotations;

namespace CustomerService.Api.Contracts;

public sealed record UpdateCustomerRequest : IValidatableObject
{
    [Required(ErrorMessage = "First name is required.")]
    [StringLength(
        100,
        MinimumLength = 2,
        ErrorMessage = "First name must contain between 2 and 100 characters.")]
    public required string FirstName { get; init; }

    [Required(ErrorMessage = "Last name is required.")]
    [StringLength(
        100,
        MinimumLength = 2,
        ErrorMessage = "Last name must contain between 2 and 100 characters.")]
    public required string LastName { get; init; }

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email format is invalid.")]
    [StringLength(
        254,
        ErrorMessage = "Email cannot exceed 254 characters.")]
    public required string Email { get; init; }

    [Required(ErrorMessage = "Document number is required.")]
    [StringLength(
        30,
        MinimumLength = 5,
        ErrorMessage = "Document number must contain between 5 and 30 characters.")]
    public required string DocumentNumber { get; init; }

    public required DateOnly BirthDate { get; init; }

    public IEnumerable<ValidationResult> Validate(
    ValidationContext validationContext)
    {
        if (BirthDate == DateOnly.MinValue)
        {
            yield return new ValidationResult(
                "Birth date must not be the default date.",
                [nameof(BirthDate)]);
        }
    }
}