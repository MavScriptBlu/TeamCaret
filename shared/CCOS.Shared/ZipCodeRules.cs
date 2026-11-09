namespace CCOS.Shared;

/// <summary>
/// The one rule for US ZIP codes (CAR-63), shared so the API and the Razor forms say the same
/// thing. Five digits ("54481") or ZIP+4 ("54481-1234"). No spaces, no letters.
/// </summary>
public static class ZipCodeRules
{
    /// <summary>The pattern for [RegularExpression]: five digits, then an optional dash and four more.</summary>
    public const string Pattern = @"^\d{5}(-\d{4})?$";

    /// <summary>The message shown under the ZIP box when the pattern doesn't match.</summary>
    public const string ErrorMessage = "Use a 5-digit ZIP code like 54481, or ZIP+4 like 54481-1234.";
}
