using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using CCOS.Web.Pages.Officer.Products;
using Xunit;

namespace CCOS.Web.Tests.Officer.Products;

public class ProductCreateInputTests
{
    [Fact]
    public void Validate_ValidPrices_ReturnsNoErrors()
    {
        // Arrange
        var input = new CreateModel.ProductCreateInput
        {
            Category = "Category1",
            Name = "Product 1",
            Price = 10.0m,
            MemberPrice = 8.0m,
            StockQuantity = 50
        };
        var validationContext = new ValidationContext(input);

        // Act
        var results = input.Validate(validationContext).ToList();

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void Validate_MemberPriceGreaterThanRegularPrice_ReturnsValidationError()
    {
        // Arrange
        var input = new CreateModel.ProductCreateInput
        {
            Category = "Category1",
            Name = "Product 1",
            Price = 10.0m,
            MemberPrice = 12.0m,
            StockQuantity = 50
        };
        var validationContext = new ValidationContext(input);

        // Act
        var results = input.Validate(validationContext).ToList();

        // Assert
        Assert.Single(results);
        Assert.Equal("the member price cannot be greater than the regular price", results[0].ErrorMessage);
        Assert.Equal(new[] { "MemberPrice" }, results[0].MemberNames);
    }

    [Fact]
    public void Validate_PricesEqual_ReturnsNoErrors()
    {
        // Arrange
        var input = new CreateModel.ProductCreateInput
        {
            Category = "Category1",
            Name = "Product 1",
            Price = 10.0m,
            MemberPrice = 10.0m,
            StockQuantity = 50
        };
        var validationContext = new ValidationContext(input);

        // Act
        var results = input.Validate(validationContext).ToList();

        // Assert
        Assert.Empty(results);
    }
}
