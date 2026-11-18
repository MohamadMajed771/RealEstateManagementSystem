
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RealEstateMarketplace.Areas.Identity.Pages.Account
{
    public class RegisterConfirmationModel : PageModel
    {
        public string Email { get; private set; } = string.Empty;

        public IActionResult OnGet(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return RedirectToPage("/Account/Register");
            }

            Email = email;

            return Page();
        }
    }
}
