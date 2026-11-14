using System.ComponentModel.DataAnnotations;
using CCOS.Web.Pages.Officer.Venues;
using CCOS.Web.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Contrib.HttpClient;

namespace CCOS.Web.Tests.Pages.Officer.Venues;

/// <summary>
/// Tests for the venue form's ZIP code rule (CAR-63): 5 digits, or ZIP+4.
/// </summary>
public class CreateModelTests
{
    // A form that passes every rule, so each test only changes the ZIP code.
    private static CreateModel BuildModel(string? zipCode)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        var httpClient = handlerMock.CreateClient();
        httpClient.BaseAddress = new Uri("http://localhost");

        return new CreateModel(new VenuesApiClient(httpClient), new Mock<ILogger<CreateModel>>().Object)
        {
            Name = "Main Hall",
            AddressLine1 = "1001 Centerpoint Dr",
            City = "Stevens Point",
            State = "WI",
            ZipCode = zipCode,
            Capacity = 100,
        };
    }

    // Runs the same attribute checks ASP.NET Core runs when the form is posted.
    private static List<ValidationResult> Validate(CreateModel model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }

    [Theory]
    [InlineData("54481")]
    [InlineData("54481-1234")]
    public void ZipCode_accepts_five_digits_and_zip_plus_four(string zipCode)
    {
        Assert.Empty(Validate(BuildModel(zipCode)));
    }

    [Theory]
    [InlineData("1234")]
    [InlineData("123456")]
    [InlineData("54481-")]
    [InlineData("54481-123")]
    [InlineData("54481 1234")]
    [InlineData("abcde")]
    public void ZipCode_rejects_bad_formats_with_a_message_on_ZipCode(string zipCode)
    {
        var results = Validate(BuildModel(zipCode));

        var problem = Assert.Single(results);
        Assert.Contains(nameof(CreateModel.ZipCode), problem.MemberNames);
        Assert.Contains("5-digit ZIP", problem.ErrorMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ZipCode_is_required(string? zipCode)
    {
        var results = Validate(BuildModel(zipCode));

        var problem = Assert.Single(results);
        Assert.Equal("ZIP code is required.", problem.ErrorMessage);
    }
}
