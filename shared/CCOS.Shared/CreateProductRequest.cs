using System;
using System.Collections.Generic;
using System.Text;

namespace CCOS.Shared
{
    /// <summary>
    /// request body for creating a new product
    /// </summary>
    public record CreateProductRequest(
        string Category,
        string Name,
        string? Description,
        decimal Price,
        decimal? MemberPrice,
        int StockQuantity);
}
