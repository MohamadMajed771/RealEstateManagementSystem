
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using RealEstateMarketplace.Models;

namespace RealEstateMarketplace.Areas.Identity.Pages.Account
{
    public class ConfirmEmailModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public ConfirmEmailModel(
            UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public bool IsConfirmed { get; private set; }

        public async Task<IActionResult> OnGetAsync(
            string? userId,
            string? code,
            string? returnUrl = null)
        {
            if (string.IsNullOrEmpty(userId) ||
                string.IsNullOrEmpty(code))
            {
                return BadRequest(
                    "Invalid email confirmation link.");
            }

            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                return NotFound();
            }

            string decodedCode;

            try
            {
                decodedCode = Encoding.UTF8.GetString(
                    WebEncoders.Base64UrlDecode(code));
            }
            catch (FormatException)
            {
                return BadRequest(
                    "Invalid email confirmation token.");
            }

            var result = await _userManager.ConfirmEmailAsync(
                user, decodedCode);

            IsConfirmed = result.Succeeded;

            return Page();
        }
    }
}
