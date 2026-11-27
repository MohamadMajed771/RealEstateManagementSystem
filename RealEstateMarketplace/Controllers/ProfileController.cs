
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealEstateMarketplace.Data;
using RealEstateMarketplace.Models;
using RealEstateMarketplace.Services;
using RealEstateMarketplace.Services.Messaging;
using SharedContracts.Events;
using RealEstateMarketplace.ViewModels;

namespace RealEstateMarketplace.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly PropertyPhotoService _photos;
        private readonly ApplicationDbContext _context;

        public ProfileController(
            UserManager<ApplicationUser> userManager,
            PropertyPhotoService photos,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _photos = photos;
            _context = context;
        }

        // POST: /Profile/UploadPicture
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(6 * 1024 * 1024)]
        public async Task<IActionResult> UploadPicture(IFormFile? picture)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (picture == null)
            {
                TempData["ErrorMessage"] = "Please choose a picture.";
                return RedirectToAction(nameof(Index));
            }

            string path;
            try
            {
                path = await _photos.UploadProfilePictureAsync(picture);
            }
            catch (ArgumentException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }

            var old = user.ProfilePicturePath;
            user.ProfilePicturePath = path;
            if (!(await _userManager.UpdateAsync(user)).Succeeded)
            {
                // Keep the old picture; throw away the new file.
                await _photos.DeleteProfilePictureAsync(path);
                TempData["ErrorMessage"] = "Your picture could not be saved. Please try again.";
                return RedirectToAction(nameof(Index));
            }
            await _photos.DeleteProfilePictureAsync(old);

            TempData["SuccessMessage"] = "Your profile picture was updated.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Profile/RemovePicture
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemovePicture()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var old = user.ProfilePicturePath;
            user.ProfilePicturePath = null;
            if (!(await _userManager.UpdateAsync(user)).Succeeded)
            {
                TempData["ErrorMessage"] = "Your picture could not be removed. Please try again.";
                return RedirectToAction(nameof(Index));
            }
            await _photos.DeleteProfilePictureAsync(old);

            TempData["SuccessMessage"] = "Your profile picture was removed.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Profile
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            return View(user);
        }

        // GET: /Profile/Edit
        [HttpGet]
        public async Task<IActionResult> Edit()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var model = new EditProfileViewModel
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                PhoneNumber = user.PhoneNumber
            };

            return View(model);
        }

        // POST: /Profile/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            EditProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var oldName = $"{user.FirstName} {user.LastName}";
            user.FirstName = model.FirstName.Trim();
            user.LastName = model.LastName.Trim();
            user.PhoneNumber = string.IsNullOrWhiteSpace(
                model.PhoneNumber)
                ? null
                : model.PhoneNumber.Trim();

            // Open transfer cases show the buyer's and seller's names: tell the
            // Transfer Service about a new name (TransferInformationUpdated).
            // Identity saves through the same DbContext, so the outbox rows are
            // stored in the same save as the profile.
            var newName = $"{user.FirstName} {user.LastName}";
            if (newName != oldName)
            {
                var open = await _context.TransferRequests
                    .AsNoTracking()
                    .Where(r => (r.Transaction.BuyerId == user.Id || r.Transaction.SellerId == user.Id) &&
                                r.Status != TransferRequestStatus.Completed &&
                                r.Status != TransferRequestStatus.Rejected &&
                                r.Status != TransferRequestStatus.Cancelled)
                    .Select(r => new { r.Id, IsBuyer = r.Transaction.BuyerId == user.Id })
                    .ToListAsync();

                foreach (var r in open)
                    Outbox.Add(_context, new TransferInformationUpdated
                    {
                        TransferRequestId = r.Id,
                        PartyRole = r.IsBuyer ? "Buyer" : "Seller",
                        FullName = newName
                    });
            }

            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] =
                    "Your profile has been updated successfully.";

                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(
                    string.Empty, error.Description);
            }

            return View(model);
        }
    }
}
