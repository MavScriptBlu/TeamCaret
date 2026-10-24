using System;
using System.Collections.Generic;
using System.Text;

namespace CCOS.Shared
{
    /// <summary>
    /// Request body for updating a product, including its active status.
    /// </summary>
    public record UpdateProductRequest(
        string Category,
        string Name,
        string? Description,
        decimal Price,
        decimal MemberPrice,
        int StockQuantity,
        bool IsActive);
}
