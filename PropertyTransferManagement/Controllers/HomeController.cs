using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PropertyTransferManagement.Data;
using PropertyTransferManagement.Models;

namespace PropertyTransferManagement.Controllers
{
    public class HomeController : Controller
    {
        // Signed-in users go straight to their workspace.
        public IActionResult Index()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                if (User.IsInRole(DataSeeder.AdminRole))
                    return RedirectToAction("Index", "Admin");

                if (User.IsInRole(DataSeeder.OfficerRole))
                    return RedirectToAction("Index", "Officer");

                return RedirectToAction("Index", "Cases");
            }

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
