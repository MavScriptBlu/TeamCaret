using CCOS.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CCOS.Web.Pages.Account;

/// <summary>
/// Log out (/Account/Logout, CAR-68). Only a POST from the nav's Log out button signs the person
/// out (so a link on another site can't), then it goes to the merch page. It also empties the
/// cart (CAR-92), so the next person on a shared computer starts with an empty one.
/// </summary>
public class LogoutModel : PageModel
{
    /// <summary>Opening the address directly does nothing but go to the merch page.</summary>
    public IActionResult OnGet() => RedirectToPage("/Products/Index");

    /// <summary>Removes the sign-in cookie (and the API tokens inside it) and empties the cart.</summary>
    public async Task<IActionResult> OnPostAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        HttpContext.ClearCart();
        return RedirectToPage("/Products/Index");
    }
}
