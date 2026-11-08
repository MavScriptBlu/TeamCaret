using System.ComponentModel.DataAnnotations;
using CCOS.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CCOS.Web.Pages.Account;

/// <summary>
/// Log in page (/Account/Login, CAR-68). Sends the email and password to CCOS.Api, then keeps
/// the person signed in with a cookie. A wrong email or password shows one general message,
/// so the page never reveals which accounts exist.
/// </summary>
public class LoginModel(AccountApiClient accounts, ILogger<LoginModel> logger) : PageModel
{
    /// <summary>Shown when the email or password is wrong.</summary>
    public const string WrongCredentialsMessage = "Email or password is incorrect.";

    [BindProperty]
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string? Email { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Password is required.")]
    [DataType(DataType.Password)]
    public string? Password { get; set; }

    /// <summary>The page to go back to after signing in (set when a locked page sent the person here).</summary>
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    /// <summary>
    /// True when a locked page sent the person here (CAR-69), so the page says "Log in first".
    /// Set by the cookie's OnRedirectToLogin in Program.cs, not by the header's Log in link.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public bool LoginFirst { get; set; }

    /// <summary>Shows the form, or skips it when the person is already signed in.</summary>
    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(AccountSession.SafeReturnUrl(ReturnUrl));
        }

        return Page();
    }

    /// <summary>
    /// Log in: redisplays the form with a message when the input is invalid, the email or password
    /// is wrong, or the API can't be reached; otherwise goes back to where the person came from.
    /// </summary>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var outcome = await AccountSession.SignInWithPasswordAsync(HttpContext, accounts, Email!, Password!, cancellationToken);

            if (outcome == AccountSession.SignInOutcome.WrongCredentials)
            {
                ModelState.AddModelError(string.Empty, WrongCredentialsMessage);
                return Page();
            }

            if (outcome == AccountSession.SignInOutcome.Failed)
            {
                logger.LogWarning("Login succeeded but the account could not be loaded from /api/me.");
                ModelState.AddModelError(string.Empty, "You couldn't be signed in. Please try again.");
                return Page();
            }
        }
        // The API is down or unreachable.
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "The sign-in API could not be reached");
            ModelState.AddModelError(string.Empty, "Signing in isn't available right now. Please try again later.");
            return Page();
        }
        // The API took too long. (A visitor closing the page is not treated as an error.)
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "The sign-in API request timed out");
            ModelState.AddModelError(string.Empty, "Signing in isn't available right now. Please try again later.");
            return Page();
        }

        return LocalRedirect(AccountSession.SafeReturnUrl(ReturnUrl));
    }
}
