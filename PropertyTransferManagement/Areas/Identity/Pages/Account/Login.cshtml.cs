using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PropertyTransferManagement.Models;
using PropertyTransferManagement.Services.Identity;

namespace PropertyTransferManagement.Areas.Identity.Pages.Account
{
    /// <summary>
    /// Login. An unverified account is sent to the code page (with a fresh
    /// code), and deactivated accounts cannot sign in.
    /// </summary>
    [AllowAnonymous]
    public class LoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly EmailVerificationService _verification;

        public LoginModel(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager,
            EmailVerificationService verification)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _verification = verification;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string? ReturnUrl { get; set; }

        public class InputModel
        {
            [Required, EmailAddress]
            public string Email { get; set; } = string.Empty;

            [Required, DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string? returnUrl = null)
        {
            ReturnUrl = returnUrl;
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            // Only go back to pages of this site; anything else goes home.
            returnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : Url.Content("~/");
            ReturnUrl = returnUrl;
            if (!ModelState.IsValid) return Page();

            var user = await _userManager.FindByEmailAsync(Input.Email.Trim());
            if (user != null && (!user.IsActive || !user.EmailConfirmed))
            {
                // These messages say the account exists, so show them only after the
                // password is right. (Identity itself answers "not allowed" before it
                // checks the password.)
                if (await _userManager.IsLockedOutAsync(user)) return RedirectToPage("./Lockout");
                if (!await _userManager.CheckPasswordAsync(user, Input.Password))
                {
                    await _userManager.AccessFailedAsync(user); // still counts toward lockout
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    return Page();
                }

                if (!user.IsActive)
                {
                    ModelState.AddModelError(string.Empty, "This account is deactivated. Contact the Transfer Service administrator.");
                    return Page();
                }

                // Right password, email not verified yet: send a code and go to the code page.
                await _verification.SendCodeAsync(user);
                TempData["VerifyEmail"] = user.Email;
                return RedirectToPage("./VerifyEmail", new { sent = true });
            }

            var result = await _signInManager.PasswordSignInAsync(
                user?.UserName ?? Input.Email.Trim(), Input.Password, Input.RememberMe, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                user!.LastLoginAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);
                return LocalRedirect(returnUrl);
            }

            if (result.IsLockedOut)
                return RedirectToPage("./Lockout");

            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return Page();
        }
    }
}
