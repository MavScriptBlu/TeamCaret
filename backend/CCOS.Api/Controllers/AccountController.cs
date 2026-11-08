using System.Security.Claims;
using CCOS.Repositories;
using CCOS.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CCOS.Api.Controllers;

/// <summary>
/// Account endpoints (CAR-67): creating an account, and "who am I?". Signing in, refreshing the
/// token and password resets come from Identity's built-in endpoints under /api/auth (Program.cs).
/// </summary>
[ApiController]
[Route("api")]
public class AccountController(AccountRepository accounts, ClubClock clock) : ControllerBase
{
    /// <summary>
    /// Shown when the email already has an account. Identity's own wording ("Username '...' is
    /// already taken") talks about usernames, which people never see on our site.
    /// </summary>
    public const string EmailTakenMessage = "An account with that email already exists.";

    /// <summary>
    /// POST /api/account/register: creates the person (Customer) and their login in one
    /// transaction. Missing fields come back as 400 automatically ([ApiController]); an email
    /// that's already used or a weak password comes back as 400 with the message on that field.
    /// </summary>
    [HttpPost("account/register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterAccountRequest input,
        CancellationToken cancellationToken)
    {
        var registration = await accounts.RegisterAsync(
            input.FirstName,
            input.LastName,
            input.Email,
            input.Password,
            cancellationToken);

        if (!registration.Succeeded)
        {
            // Put each Identity error on the field it's about, so the website can show it in the right place.
            // A used email gives Identity two errors (the username is the email, and RequireUniqueEmail),
            // so both become one plain message.
            var emailTakenAdded = false;
            foreach (var error in registration.Errors)
            {
                if (error.Code is "DuplicateUserName" or "DuplicateEmail")
                {
                    if (!emailTakenAdded)
                    {
                        ModelState.AddModelError(nameof(input.Email), EmailTakenMessage);
                        emailTakenAdded = true;
                    }

                    continue;
                }

                var field = error.Code.StartsWith("Password", StringComparison.Ordinal)
                    ? nameof(input.Password)
                    : nameof(input.Email);
                ModelState.AddModelError(field, error.Description);
            }

            return ValidationProblem(ModelState);
        }

        return Ok();
    }

    /// <summary>
    /// GET /api/me: who's signed in, with their member and officer status today (club date).
    /// 401 when nobody is signed in.
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<CurrentAccountDto>> Me(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            return Unauthorized();
        }

        var profile = await accounts.GetProfileAsync(userId, clock.Today, cancellationToken);
        if (profile is null)
        {
            return Unauthorized();
        }

        return profile;
    }
}
