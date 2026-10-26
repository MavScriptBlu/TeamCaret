using System.ComponentModel.DataAnnotations;
using CCOS.Shared;

namespace CCOS.Data.Tests;

/// <summary>
/// Tests for CreateProductRequest and UpdateProductRequest validation rules.
/// These are the "returns 400" cases from the AC, just tested at the DTO level
/// since that's where the validation attributes live.
/// </summary>
public class ProductValidationTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // CreateProductRequest
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A valid CreateProductRequest passes validation.
    /// </summary>
    [Fact]
    public void Create_valid_request_passes()
    {
        var request = new CreateProductRequest(
            Category: "Swag",
            Name: "Test Product",
            Description: null,
            Price: 10m,
            MemberPrice: 8m,
            StockQuantity: 5);

        var errors = Validate(request);
        Assert.Empty(errors);
    }

    /// <summary>
    /// Blank name fails validation.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Create_blank_name_fails(string? name)
    {
        var request = new CreateProductRequest(
            Category: "Swag",
            Name: name!,
            Description: null,
            Price: 10m,
            MemberPrice: 8m,
            StockQuantity: 5);

        var errors = Validate(request);
        Assert.Contains(errors, e => e.MemberNames.Contains("Name"));
    }

    /// <summary>
    /// Blank category fails validation.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Create_blank_category_fails(string? category)
    {
        var request = new CreateProductRequest(
            Category: category!,
            Name: "Test",
            Description: null,
            Price: 10m,
            MemberPrice: 8m,
            StockQuantity: 5);

        var errors = Validate(request);
        Assert.Contains(errors, e => e.MemberNames.Contains("Category"));
    }

    /// <summary>
    /// Negative price fails validation.
    /// </summary>
    [Fact]
    public void Create_negative_price_fails()
    {
        var request = new CreateProductRequest(
            Category: "Swag",
            Name: "Test",
            Description: null,
            Price: -1m,
            MemberPrice: 0m,
            StockQuantity: 5);

        var errors = Validate(request);
        Assert.Contains(errors, e => e.MemberNames.Contains("Price"));
    }

    /// <summary>
    /// Negative member price fails validation.
    /// </summary>
    [Fact]
    public void Create_negative_member_price_fails()
    {
        var request = new CreateProductRequest(
            Category: "Swag",
            Name: "Test",
            Description: null,
            Price: 10m,
            MemberPrice: -1m,
            StockQuantity: 5);

        var errors = Validate(request);
        Assert.Contains(errors, e => e.MemberNames.Contains("MemberPrice"));
    }

    /// <summary>
    /// Member price above regular price fails validation.
    /// </summary>
    [Fact]
    public void Create_member_price_above_price_fails()
    {
        var request = new CreateProductRequest(
            Category: "Swag",
            Name: "Test",
            Description: null,
            Price: 10m,
            MemberPrice: 15m,
            StockQuantity: 5);

        var errors = Validate(request);
        Assert.Contains(errors, e => e.MemberNames.Contains("MemberPrice"));
    }

    /// <summary>
    /// Negative stock quantity fails validation.
    /// </summary>
    [Fact]
    public void Create_negative_stock_fails()
    {
        var request = new CreateProductRequest(
            Category: "Swag",
            Name: "Test",
            Description: null,
            Price: 10m,
            MemberPrice: 8m,
            StockQuantity: -1);

        var errors = Validate(request);
        Assert.Contains(errors, e => e.MemberNames.Contains("StockQuantity"));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UpdateProductRequest
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A valid UpdateProductRequest passes validation.
    /// </summary>
    [Fact]
    public void Update_valid_request_passes()
    {
        var request = new UpdateProductRequest(
            Category: "Swag",
            Name: "Test Product",
            Description: null,
            Price: 10m,
            MemberPrice: 8m,
            StockQuantity: 5,
            IsActive: true);

        var errors = Validate(request);
        Assert.Empty(errors);
    }

    /// <summary>
    /// Blank name fails validation.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Update_blank_name_fails(string? name)
    {
        var request = new UpdateProductRequest(
            Category: "Swag",
            Name: name!,
            Description: null,
            Price: 10m,
            MemberPrice: 8m,
            StockQuantity: 5,
            IsActive: true);

        var errors = Validate(request);
        Assert.Contains(errors, e => e.MemberNames.Contains("Name"));
    }

    /// <summary>
    /// Blank category fails validation.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Update_blank_category_fails(string? category)
    {
        var request = new UpdateProductRequest(
            Category: category!,
            Name: "Test",
            Description: null,
            Price: 10m,
            MemberPrice: 8m,
            StockQuantity: 5,
            IsActive: true);

        var errors = Validate(request);
        Assert.Contains(errors, e => e.MemberNames.Contains("Category"));
    }

    /// <summary>
    /// Negative price fails validation.
    /// </summary>
    [Fact]
    public void Update_negative_price_fails()
    {
        var request = new UpdateProductRequest(
            Category: "Swag",
            Name: "Test",
            Description: null,
            Price: -1m,
            MemberPrice: 0m,
            StockQuantity: 5,
            IsActive: true);

        var errors = Validate(request);
        Assert.Contains(errors, e => e.MemberNames.Contains("Price"));
    }

    /// <summary>
    /// Negative member price fails validation.
    /// </summary>
    [Fact]
    public void Update_negative_member_price_fails()
    {
        var request = new UpdateProductRequest(
            Category: "Swag",
            Name: "Test",
            Description: null,
            Price: 10m,
            MemberPrice: -1m,
            StockQuantity: 5,
            IsActive: true);

        var errors = Validate(request);
        Assert.Contains(errors, e => e.MemberNames.Contains("MemberPrice"));
    }

    /// <summary>
    /// Member price above regular price fails validation.
    /// </summary>
    [Fact]
    public void Update_member_price_above_price_fails()
    {
        var request = new UpdateProductRequest(
            Category: "Swag",
            Name: "Test",
            Description: null,
            Price: 10m,
            MemberPrice: 15m,
            StockQuantity: 5,
            IsActive: true);

        var errors = Validate(request);
        Assert.Contains(errors, e => e.MemberNames.Contains("MemberPrice"));
    }

    /// <summary>
    /// Negative stock quantity fails validation.
    /// </summary>
    [Fact]
    public void Update_negative_stock_fails()
    {
        var request = new UpdateProductRequest(
            Category: "Swag",
            Name: "Test",
            Description: null,
            Price: 10m,
            MemberPrice: 8m,
            StockQuantity: -1,
            IsActive: true);

        var errors = Validate(request);
        Assert.Contains(errors, e => e.MemberNames.Contains("StockQuantity"));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helper
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Runs DataAnnotations validation on an object and returns all errors.
    /// </summary>
    private static List<ValidationResult> Validate(object model)
    {
        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return results;
    }
}
