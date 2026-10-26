using System.ComponentModel.DataAnnotations;

namespace CCOS.Shared;

/// <summary>
/// Request body for POST /api/products. Creates a new product with IsActive defaulting to true.
/// Validation: name and category required, prices >= 0, member price &lt;= price, stock >= 0.
/// </summary>
/// <param name="Category">Which group the product belongs to, like Swag or Resources.</param>
/// <param name="Name">The product name.</param>
/// <param name="Description">Optional text describing the product.</param>
/// <param name="Price">The regular price.</param>
/// <param name="MemberPrice">The club member price.</param>
/// <param name="StockQuantity">How many are on hand.</param>
public record CreateProductRequest(
    [property: Required(ErrorMessage = "Category is required.")]
    [property: MinLength(1, ErrorMessage = "Category is required.")]
    string Category,

    [property: Required(ErrorMessage = "Name is required.")]
    [property: MinLength(1, ErrorMessage = "Name is required.")]
    string Name,

    string? Description,

    [property: Range(0, double.MaxValue, ErrorMessage = "Price must be at least 0.")]
    decimal Price,

    [property: Range(0, double.MaxValue, ErrorMessage = "Member price must be at least 0.")]
    decimal MemberPrice,

    [property: Range(0, int.MaxValue, ErrorMessage = "Stock quantity must be at least 0.")]
    int StockQuantity) : IValidatableObject
{
    // Built once instead of on every validation run.
    private static readonly string[] MemberPriceFieldNames = [nameof(MemberPrice)];

    /// <summary>
    /// Cross-field validation: MemberPrice must not exceed Price.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MemberPrice > Price)
        {
            yield return new ValidationResult(
                "Member price cannot exceed regular price.",
                MemberPriceFieldNames);
        }
    }
}
