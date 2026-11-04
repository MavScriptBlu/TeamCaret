using System.ComponentModel.DataAnnotations;
using System.Net;
using CCOS.Shared;
using CCOS.Web.Extensions;
using CCOS.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CCOS.Web.Pages.Account;

/// <summary>
/// Create account page (/Account/Register, CAR-68). Sends the new account to CCOS.Api, which
/// creates the person (Customer) and their login together, then signs them in right away.
/// Messages from the API (email already used, weak password) show next to the matching field.
/// </summary>
public class RegisterModel(AccountApiClient accounts, ILogger<RegisterModel> logger) : PageModel
{
    [BindProperty]
    [Required(ErrorMessage = "First name is required.")]
    [StringLength(50, ErrorMessage = "First name can be at most 50 characters.")]
    public string? FirstName { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Last name is required.")]
    [StringLength(50, ErrorMessage = "Last name can be at most 50 characters.")]
    public string? LastName { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(256, ErrorMessage = "Email can be at most 256 characters.")]
    public string? Email { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Password is required.")]
    [DataType(DataType.Password)]
    public string? Password { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Please type your password again.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "The passwords don't match.")]
    public string? ConfirmPassword { get; set; }

    /// <summary>The page to go back to after creating the account.</summary>
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    /// <summary>Shows the blank form, or skips it when the person is already signed in.</summary>
    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(AccountSession.SafeReturnUrl(ReturnUrl));
        }

        return Page();
    }

    /// <summary>
    /// Create account: redisplays the form with messages when the input or the API rejects it,
    /// otherwise signs the new person in and goes back to where they came from.
    /// </summary>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var request = new RegisterAccountRequest(FirstName!.Trim(), LastName!.Trim(), Email!.Trim(), Password!);

        try
        {
            using (var response = await accounts.RegisterAsync(request, cancellationToken))
            {
                // The API rejected the values (email already used, weak password):
                // show its messages next to the matching fields.
                if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity)
                {
                    await ModelState.AddApiValidationErrorsAsync(
                        response,
                        string.Empty,
                        "Your account could not be created. Please check the values and try again.",
                        cancellationToken);

                    return Page();
                }

                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("Failed to create an account. Status code: {StatusCode}", response.StatusCode);
                    ModelState.AddModelError(string.Empty, "Your account could not be created. Please try again.");
                    return Page();
                }
            }

            // The account exists now: sign the person in so they don't have to type it all again.
            var outcome = await AccountSession.SignInWithPasswordAsync(HttpContext, accounts, Email!, Password!, cancellationToken);
            if (outcome != AccountSession.SignInOutcome.SignedIn)
            {
                logger.LogWarning("A new account was created but signing in afterward failed: {Outcome}", outcome);
                return RedirectToPage("/Account/Login", new { returnUrl = ReturnUrl });
            }
        }
        // The API is down or unreachable.
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "The account API could not be reached");
            ModelState.AddModelError(string.Empty, "Creating an account isn't available right now. Please try again later.");
            return Page();
        }
        // The API took too long. (A visitor closing the page is not treated as an error.)
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "The account API request timed out");
            ModelState.AddModelError(string.Empty, "Creating an account isn't available right now. Please try again later.");
            return Page();
        }

        return LocalRedirect(AccountSession.SafeReturnUrl(ReturnUrl));
    }
}
