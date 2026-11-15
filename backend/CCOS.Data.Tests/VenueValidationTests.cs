using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using CCOS.Shared;

namespace CCOS.Data.Tests;

/// <summary>
/// Tests for CreateVenueRequest and UpdateVenueRequest ZIP code validation rules (CAR-63).
/// </summary>
public class VenueValidationTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // CreateVenueRequest ZIP code validation tests
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Valid 5-digit ZIP and valid ZIP+4 pass validation on CreateVenueRequest.
    /// </summary>
    [Theory]
    [InlineData("54401")]
    [InlineData("54481")]
    [InlineData("54401-1234")]
    [InlineData("90210-9999")]
    public void Create_valid_zip_code_passes(string zipCode)
    {
        var request = CreateValidRequest() with { ZipCode = zipCode };
        var errors = Validate(request);
        Assert.Empty(errors);
    }

    /// <summary>
    /// Invalid ZIP codes (letters, 4 digits, 6 digits, 5440A, symbols) fail validation on CreateVenueRequest.
    /// </summary>
    [Theory]
    [InlineData("ABCDE")]
    [InlineData("5440")]
    [InlineData("544012")]
    [InlineData("5440A")]
    [InlineData("54401-123")]
    [InlineData("54401-12345")]
    [InlineData("54401 1234")]
    [InlineData("54401_1234")]
    public void Create_invalid_zip_code_fails(string zipCode)
    {
        var request = CreateValidRequest() with { ZipCode = zipCode };
        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CreateVenueRequest.ZipCode)) &&
                                     e.ErrorMessage == ZipCodeRules.ErrorMessage);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UpdateVenueRequest ZIP code validation tests
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Valid 5-digit ZIP and valid ZIP+4 pass validation on UpdateVenueRequest.
    /// </summary>
    [Theory]
    [InlineData("54401")]
    [InlineData("54481")]
    [InlineData("54401-1234")]
    [InlineData("90210-9999")]
    public void Update_valid_zip_code_passes(string zipCode)
    {
        var request = UpdateValidRequest() with { ZipCode = zipCode };
        var errors = Validate(request);
        Assert.Empty(errors);
    }

    /// <summary>
    /// Invalid ZIP codes fail validation on UpdateVenueRequest.
    /// </summary>
    [Theory]
    [InlineData("ABCDE")]
    [InlineData("5440")]
    [InlineData("544012")]
    [InlineData("5440A")]
    [InlineData("54401-123")]
    [InlineData("54401-12345")]
    [InlineData("54401 1234")]
    [InlineData("54401_1234")]
    public void Update_invalid_zip_code_fails(string zipCode)
    {
        var request = UpdateValidRequest() with { ZipCode = zipCode };
        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(UpdateVenueRequest.ZipCode)) &&
                                     e.ErrorMessage == ZipCodeRules.ErrorMessage);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helper Methods
    // ─────────────────────────────────────────────────────────────────────────

    private static CreateVenueRequest CreateValidRequest() => new(
        Name: "Main Campus, Room 101",
        AddressLine1: "500 32nd St N",
        AddressLine2: null,
        City: "Wisconsin Rapids",
        State: "WI",
        ZipCode: "54494",
        Capacity: 50);

    private static UpdateVenueRequest UpdateValidRequest() => new(
        Name: "Main Campus, Room 101",
        AddressLine1: "500 32nd St N",
        AddressLine2: null,
        City: "Wisconsin Rapids",
        State: "WI",
        ZipCode: "54494",
        Capacity: 50,
        IsActive: true);

    /// <summary>
    /// Runs the same validation ASP.NET Core runs on a request and returns all errors.
    /// The request records keep their rules (like [Required]) on the constructor parameters,
    /// because that's the only place ASP.NET Core accepts them on a record; it throws if
    /// they're on the properties. Validator.TryValidateObject only reads properties, so this
    /// checks each constructor parameter's rules against the matching property's value.
    /// </summary>
    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        var type = model.GetType();
        var constructor = type.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();

        foreach (var parameter in constructor.GetParameters())
        {
            var rules = parameter.GetCustomAttributes(typeof(ValidationAttribute), inherit: true)
                .Cast<ValidationAttribute>();
            var value = type.GetProperty(parameter.Name!)!.GetValue(model);
            var context = new ValidationContext(model) { MemberName = parameter.Name };
            Validator.TryValidateValue(value!, context, results, rules);
        }

        return results;
    }
}
