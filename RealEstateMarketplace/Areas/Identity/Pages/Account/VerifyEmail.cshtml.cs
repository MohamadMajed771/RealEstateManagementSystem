using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RealEstateMarketplace.Models;
using RealEstateMarketplace.Services.Identity;

namespace RealEstateMarketplace.Areas.Identity.Pages.Account
{
    public class VerifyEmailModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly EmailVerificationService _verification;

        public VerifyEmailModel(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager,
            EmailVerificationService verification)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _verification = verification;
        }

        [BindProperty, Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [BindProperty, Display(Name = "Verification code")]
        public string? Code { get; set; }

        public string? Message { get; set; }

        public void OnGet(string? email, bool sent = false)
        {
            Email = email ?? string.Empty;
            if (sent) Message = "We sent a 6-digit code to your email address.";
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(Code))
            {
                ModelState.AddModelError(nameof(Code), "Enter the 6-digit code from the email.");
                return Page();
            }

            var user = await _userManager.FindByEmailAsync(Email.Trim());
            if (user == null)
            {
                ModelState.AddModelError(nameof(Code), "The code is incorrect or has expired.");
                return Page();
            }

            if (user.EmailConfirmed)
                return RedirectToPage("./Login");

            switch (await _verification.VerifyAsync(user, Code))
            {
                case VerifyResult.Verified:
                    await _signInManager.SignInAsync(user, isPersistent: false);
                    TempData["SuccessMessage"] = "Your email is verified and your account is active. Welcome!";
                    return RedirectToAction("Index", "Profile", new { area = "" });

                case VerifyResult.LockedOut:
                    ModelState.AddModelError(nameof(Code),
                        "Too many wrong codes. Wait a few minutes, then request a new code.");
                    return Page();

                default:
                    ModelState.AddModelError(nameof(Code),
                        "The code is incorrect or has expired. Try again or request a new code.");
                    return Page();
            }
        }

        public async Task<IActionResult> OnPostResendAsync()
        {
            ModelState.Clear();
            var user = string.IsNullOrWhiteSpace(Email) ? null : await _userManager.FindByEmailAsync(Email.Trim());

            // Same answer whether or not the account exists
            if (user != null && !user.EmailConfirmed && !await _verification.SendCodeAsync(user))
            {
                Message = "A code was sent less than a minute ago. Please check your inbox (and spam folder).";
                return Page();
            }

            Message = "If an unverified account uses this email, a new code is on its way.";
            return Page();
        }
    }
}
