using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PropertyTransferManagement.Models;
using PropertyTransferManagement.Services.Identity;

namespace PropertyTransferManagement.Areas.Identity.Pages.Account
{
    /// <summary>
    /// Registration (spec §20): email + password, then a 6-digit code is
    /// emailed and must be entered before the account can sign in.
    /// </summary>
    [AllowAnonymous]
    public class RegisterModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly EmailVerificationService _verification;

        public RegisterModel(UserManager<ApplicationUser> userManager, EmailVerificationService verification)
        {
            _userManager = userManager;
            _verification = verification;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required, StringLength(50, MinimumLength = 2), Display(Name = "First name")]
            public string FirstName { get; set; } = string.Empty;

            [Required, StringLength(50, MinimumLength = 2), Display(Name = "Last name")]
            public string LastName { get; set; } = string.Empty;

            [Required, EmailAddress]
            public string Email { get; set; } = string.Empty;

            [Required, StringLength(100, MinimumLength = 6, ErrorMessage = "The {0} must be at least {2} characters long.")]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            [DataType(DataType.Password), Display(Name = "Confirm password")]
            [Compare(nameof(Password), ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; } = string.Empty;
        }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var email = Input.Email.Trim();
            IdentityResult result;
            var user = await _userManager.FindByEmailAsync(email);
            if (user != null && !user.EmailConfirmed)
            {
                // Nobody has proven they own this email yet, so a new sign-up takes
                // the account over (the code still only goes to the real inbox).
                // This stops a stranger from blocking an email by registering it first.
                // The security stamp is kept, so a code already sent to this inbox
                // (codes are limited to one a minute) still works.
                user.FirstName = Input.FirstName.Trim();
                user.LastName = Input.LastName.Trim();
                result = IdentityResult.Success;
                foreach (var validator in _userManager.PasswordValidators)
                {
                    result = await validator.ValidateAsync(_userManager, user, Input.Password);
                    if (!result.Succeeded) break;
                }
                if (result.Succeeded)
                {
                    user.PasswordHash = _userManager.PasswordHasher.HashPassword(user, Input.Password);
                    result = await _userManager.UpdateAsync(user);
                }
            }
            else
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    FirstName = Input.FirstName.Trim(),
                    LastName = Input.LastName.Trim(),
                    CreatedAt = DateTime.UtcNow
                };
                result = await _userManager.CreateAsync(user, Input.Password);
            }

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return Page();
            }

            await _verification.SendCodeAsync(user);

            TempData["VerifyEmail"] = email;
            return RedirectToPage("./VerifyEmail", new { sent = true });
        }
    }
}
