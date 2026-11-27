using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PropertyTransferManagement.Areas.Identity.Pages.Account
{
    /// <summary>Old link-based page: codes are used instead.</summary>
    [AllowAnonymous]
    public class ResendEmailConfirmationModel : PageModel
    {
        public IActionResult OnGet() => RedirectToPage("./VerifyEmail");
    }
}
