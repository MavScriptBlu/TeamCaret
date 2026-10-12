namespace CCOS.Shared;

/// <summary>
/// A product as returned by GET /api/products (see docs/CAR-1_Product_CRUD_Design.md).
/// </summary>
/// <param name="MemberPrice">
/// Null when the caller may not see member pricing. Per the CAR-1 design, customer-facing
/// responses only include it for a current member.
/// </param>
public record ProductDto(
    int ProductId,
    string Category,
    string Name,
    string? Description,
    decimal Price,
    decimal? MemberPrice,
    int StockQuantity,
    bool IsActive);
