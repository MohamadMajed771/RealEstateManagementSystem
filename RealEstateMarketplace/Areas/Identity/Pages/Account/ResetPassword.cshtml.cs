using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RealEstateMarketplace.Data;
using RealEstateMarketplace.Services.Identity;

namespace RealEstateMarketplace.Areas.Identity.Pages.Account;

/// <summary>
/// Step 2 of password recovery: the user enters the emailed code and
/// the new password twice. Wrong codes count towards account lockout.
/// </summary>
[AllowAnonymous]
public class ResetPasswordModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly EmailVerificationService _codes;

    public ResetPasswordModel(UserManager<ApplicationUser> userManager, EmailVerificationService codes)
    {
        _userManager = userManager;
        _codes = codes;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? Message { get; set; }

    public class InputModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Display(Name = "6-digit code")]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "New password")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm new password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string? ConfirmPassword { get; set; }
    }

    public void OnGet(bool resent = false)
    {
        Input.Email = TempData["ResetEmail"] as string ?? string.Empty;
        Message = resent
            ? "If the account exists, a new code was sent (at most one per minute)."
            : "If an account exists for this email, we sent it a 6-digit code.";
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var user = await _userManager.FindByEmailAsync(Input.Email);
        if (user == null || !await _userManager.IsEmailConfirmedAsync(user))
        {
            // Same answer as a wrong code: do not reveal whether the account exists.
            ModelState.AddModelError(string.Empty, "The code is incorrect or has expired. Try again or request a new code.");
            return Page();
        }

        var (result, errors) = await _codes.ResetPasswordAsync(user, Input.Code, Input.Password);
        switch (result)
        {
            case VerifyResult.Verified:
                return RedirectToPage("./ResetPasswordConfirmation");

            case VerifyResult.LockedOut:
                ModelState.AddModelError(string.Empty, "Too many wrong codes. Please try again later.");
                return Page();

            default:
                if (errors.Any())
                    foreach (var e in errors) ModelState.AddModelError(string.Empty, e.Description);
                else
                    ModelState.AddModelError(string.Empty, "The code is incorrect or has expired. Try again or request a new code.");
                return Page();
        }
    }

    public async Task<IActionResult> OnPostResendAsync()
    {
        var user = string.IsNullOrWhiteSpace(Input.Email) ? null : await _userManager.FindByEmailAsync(Input.Email);
        if (user != null && await _userManager.IsEmailConfirmedAsync(user))
            await _codes.SendResetCodeAsync(user);

        TempData["ResetEmail"] = Input.Email;
        return RedirectToPage(new { resent = true });
    }
}
