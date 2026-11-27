
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using RealEstateMarketplace.Models;
using RealEstateMarketplace.Services.Identity;

namespace RealEstateMarketplace.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly EmailVerificationService _verification;
        private readonly ILogger<RegisterModel> _logger;

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            EmailVerificationService verification,
            ILogger<RegisterModel> logger)
        {
            _userManager = userManager;
            _verification = verification;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string? ReturnUrl { get; set; }

        public class InputModel
        {
            [Required]
            [StringLength(50, MinimumLength = 2)]
            [Display(Name = "First Name")]
            public string FirstName { get; set; } = string.Empty;

            [Required]
            [StringLength(50, MinimumLength = 2)]
            [Display(Name = "Last Name")]
            public string LastName { get; set; } = string.Empty;

            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; } = string.Empty;

            [Required]
            [StringLength(100, MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; } = string.Empty;

            [Required]
            [DataType(DataType.Password)]
            [Compare(nameof(Password),
                ErrorMessage = "Passwords do not match.")]
            [Display(Name = "Confirm Password")]
            public string ConfirmPassword { get; set; } = string.Empty;
        }

        public void OnGet(string? returnUrl = null)
        {
            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(
            string? returnUrl = null)
        {
            ReturnUrl = returnUrl;

            if (!ModelState.IsValid)
            {
                return Page();
            }

            IdentityResult result;
            var user = await _userManager.FindByEmailAsync(Input.Email.Trim());
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
                    UserName = Input.Email.Trim(),
                    Email = Input.Email.Trim(),
                    FirstName = Input.FirstName.Trim(),
                    LastName = Input.LastName.Trim(),
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };
                result = await _userManager.CreateAsync(user, Input.Password);
            }

            if (result.Succeeded)
            {
                _logger.LogInformation(
                    "A new marketplace user registered.");

                // No privileged role is assigned during registration.

                // Spec §2: a verification code is emailed and typed on
                // the next page; the account is active once it matches.
                await _verification.SendCodeAsync(user);

                return RedirectToPage(
                    "/Account/VerifyEmail",
                    new { email = user.Email, sent = true });
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(
                    string.Empty, error.Description);
            }

            return Page();
        }
    }
}
