
/// <summary>
/// Provides methods for formatting monetary values.
/// </summary>
using System.Globalization;

namespace CCOS.Web.Services;

/// <summary>
/// Price text shared by the product pages.
/// </summary>
public static class Money
{
    private static readonly CultureInfo UsDollars = CultureInfo.GetCultureInfo("en-US");

    /// <summary>
    /// "$35.00", or "Free" for $0.
    /// </summary>
    public static string Format(decimal price) =>
        price == 0 ? "Free" : price.ToString("C", UsDollars);
}
