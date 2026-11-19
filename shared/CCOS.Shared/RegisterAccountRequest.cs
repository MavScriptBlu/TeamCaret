using System.ComponentModel.DataAnnotations;

namespace CCOS.Shared;

/// <summary>
/// Request body for POST /api/account/register (CAR-67). Creates the person (a Customer) and their
/// login together, so there's never a login without a customer. Lengths match the Customers columns;
/// password strength is checked by ASP.NET Core Identity.
/// </summary>
/// <param name="FirstName">First name.</param>
/// <param name="LastName">Last name.</param>
/// <param name="Email">Email address, also used to sign in.</param>
/// <param name="Password">Password for the new login.</param>
public record RegisterAccountRequest(
    [Required(ErrorMessage = "First name is required.")]
    [StringLength(50, ErrorMessage = "First name can be at most 50 characters.")]
    string FirstName,

    [Required(ErrorMessage = "Last name is required.")]
    [StringLength(50, ErrorMessage = "Last name can be at most 50 characters.")]
    string LastName,

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(256, ErrorMessage = "Email can be at most 256 characters.")]
    string Email,

    [Required(ErrorMessage = "Password is required.")]
    string Password);
