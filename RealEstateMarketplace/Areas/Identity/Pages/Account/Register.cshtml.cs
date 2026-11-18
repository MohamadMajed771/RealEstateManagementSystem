
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using RealEstateMarketplace.Models;

namespace RealEstateMarketplace.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<RegisterModel> _logger;

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            IEmailSender emailSender,
            ILogger<RegisterModel> logger)
        {
            _userManager = userManager;
            _emailSender = emailSender;
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

            var user = new ApplicationUser
            {
                UserName = Input.Email.Trim(),
                Email = Input.Email.Trim(),
                FirstName = Input.FirstName.Trim(),
                LastName = Input.LastName.Trim(),
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            var result = await _userManager.CreateAsync(
                user, Input.Password);

            if (result.Succeeded)
            {
                _logger.LogInformation(
                    "A new marketplace user registered.");

                // No privileged role is assigned during registration.

                var userId = await _userManager.GetUserIdAsync(user);

                var code = await _userManager
                    .GenerateEmailConfirmationTokenAsync(user);

                code = WebEncoders.Base64UrlEncode(
                    Encoding.UTF8.GetBytes(code));

                var callbackUrl = Url.Page(
                    "/Account/ConfirmEmail",
                    pageHandler: null,
                    values: new
                    {
                        area = "Identity",
                        userId,
                        code,
                        returnUrl
                    },
                    protocol: Request.Scheme);

                if (string.IsNullOrEmpty(callbackUrl))
                {
                    _logger.LogError(
                        "Could not generate email confirmation URL.");

                    return RedirectToPage(
                        "/Account/RegisterConfirmation",
                        new { email = user.Email });
                }

                await _emailSender.SendEmailAsync(
                    user.Email!,
                    "Confirm your RealEstateMarketplace account",
                    $"Please confirm your account by " +
                    $"<a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>" +
                    "clicking here</a>.");

                return RedirectToPage(
                    "/Account/RegisterConfirmation",
                    new { email = user.Email });
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
