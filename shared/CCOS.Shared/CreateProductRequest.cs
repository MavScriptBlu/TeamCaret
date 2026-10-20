using System.ComponentModel.DataAnnotations;

namespace CCOS.Shared;

/// <summary>
/// Request body for POST /api/products. Creates a new product with IsActive defaulting to true.
/// Validation: name and category required, prices >= 0, member price &lt;= price, stock >= 0.
/// </summary>
public record CreateProductRequest(
    [Required(ErrorMessage = "Category is required.")]
    [MinLength(1, ErrorMessage = "Category is required.")]
    string Category,

    [Required(ErrorMessage = "Name is required.")]
    [MinLength(1, ErrorMessage = "Name is required.")]
    string Name,

    string? Description,

    [Range(0, double.MaxValue, ErrorMessage = "Price must be at least 0.")]
    decimal Price,

    [Range(0, double.MaxValue, ErrorMessage = "Member price must be at least 0.")]
    decimal MemberPrice,

    [Range(0, int.MaxValue, ErrorMessage = "Stock quantity must be at least 0.")]
    int StockQuantity) : IValidatableObject
{
    /// <summary>
    /// Cross-field validation: MemberPrice must not exceed Price.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MemberPrice > Price)
        {
            yield return new ValidationResult(
                "Member price cannot exceed regular price.",
                [nameof(MemberPrice)]);
        }
    }
}
