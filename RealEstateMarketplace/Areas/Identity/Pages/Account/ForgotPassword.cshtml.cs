using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RealEstateMarketplace.Data;
using RealEstateMarketplace.Services.Identity;

namespace RealEstateMarketplace.Areas.Identity.Pages.Account;

/// <summary>
/// Step 1 of password recovery: email a 6-digit reset code,
/// then continue on the Reset Password page.
/// </summary>
[AllowAnonymous]
public class ForgotPasswordModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly EmailVerificationService _codes;

    public ForgotPasswordModel(UserManager<ApplicationUser> userManager, EmailVerificationService codes)
    {
        _userManager = userManager;
        _codes = codes;
    }

    [BindProperty]
    public InputModel Input { get; set; } = default!;

    public class InputModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = default!;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var user = await _userManager.FindByEmailAsync(Input.Email);

        // Only confirmed accounts get a code. The next page looks the same
        // either way, so it does not reveal whether the email exists.
        if (user != null && await _userManager.IsEmailConfirmedAsync(user))
            await _codes.SendResetCodeAsync(user);

        return RedirectToPage("./ResetPassword", new { email = Input.Email });
    }
}
