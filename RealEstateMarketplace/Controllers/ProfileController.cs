
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RealEstateMarketplace.Models;
using RealEstateMarketplace.Services;
using RealEstateMarketplace.ViewModels;

namespace RealEstateMarketplace.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly PropertyPhotoService _photos;

        public ProfileController(
            UserManager<ApplicationUser> userManager,
            PropertyPhotoService photos)
        {
            _userManager = userManager;
            _photos = photos;
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

            user.FirstName = model.FirstName.Trim();
            user.LastName = model.LastName.Trim();
            user.PhoneNumber = string.IsNullOrWhiteSpace(
                model.PhoneNumber)
                ? null
                : model.PhoneNumber.Trim();

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
