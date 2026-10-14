namespace CCOS.Shared;

/// <summary>
/// Input for creating or updating a product (POST/PUT /api/products).
/// </summary>
public record ProductInputDto(
    string Category,
    string Name,
    string? Description,
    decimal Price,
    decimal MemberPrice,
    int StockQuantity);
