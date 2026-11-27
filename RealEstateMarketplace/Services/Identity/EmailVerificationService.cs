using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Caching.Memory;
using RealEstateMarketplace.Models;

namespace RealEstateMarketplace.Services.Identity
{
    /// <summary>
    /// 6-digit email verification codes (Identity's email TOTP provider,
    /// valid for a few minutes). Wrong codes count as failed access
    /// attempts, so Identity lockout stops brute forcing.
    /// </summary>
    public class EmailVerificationService
    {
        public const string Purpose = "EmailVerification";
        private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly IMemoryCache _cache;

        public EmailVerificationService(UserManager<ApplicationUser> userManager, IEmailSender emailSender, IMemoryCache cache)
        {
            _userManager = userManager;
            _emailSender = emailSender;
            _cache = cache;
        }

        /// <returns>false when a code was sent less than a minute ago.</returns>
        public async Task<bool> SendCodeAsync(ApplicationUser user)
        {
            var key = "verify-email-sent:" + user.Id;
            if (_cache.TryGetValue(key, out _)) return false;

            var code = await _userManager.GenerateUserTokenAsync(user, TokenOptions.DefaultEmailProvider, Purpose);
            _cache.Set(key, true, ResendCooldown);

            await _emailSender.SendEmailAsync(user.Email!, "Your RealEstateMarketplace verification code",
                $"<p>Hello {HtmlEncoder.Default.Encode(user.FirstName)},</p>" +
                $"<p>Your verification code is:</p><p style='font-size:28px;font-weight:bold;letter-spacing:6px'>{code}</p>" +
                "<p>The code expires in a few minutes. If you did not create an account, ignore this email.</p>");
            return true;
        }

        public async Task<VerifyResult> VerifyAsync(ApplicationUser user, string code)
        {
            if (await _userManager.IsLockedOutAsync(user)) return VerifyResult.LockedOut;

            code = new string((code ?? "").Where(char.IsDigit).ToArray());
            bool ok = code.Length == 6 &&
                      await _userManager.VerifyUserTokenAsync(user, TokenOptions.DefaultEmailProvider, Purpose, code);

            if (!ok)
            {
                await _userManager.AccessFailedAsync(user);
                return await _userManager.IsLockedOutAsync(user) ? VerifyResult.LockedOut : VerifyResult.WrongCode;
            }

            user.EmailConfirmed = true;
            await _userManager.UpdateAsync(user);
            await _userManager.ResetAccessFailedCountAsync(user);
            return VerifyResult.Verified;
        }

        // ---------- Password reset (same 6-digit code mechanism) ----------

        public const string ResetPurpose = "PasswordReset";

        /// <returns>false when a code was sent less than a minute ago.</returns>
        public async Task<bool> SendResetCodeAsync(ApplicationUser user)
        {
            var key = "reset-code-sent:" + user.Id;
            if (_cache.TryGetValue(key, out _)) return false;

            var code = await _userManager.GenerateUserTokenAsync(user, TokenOptions.DefaultEmailProvider, ResetPurpose);
            _cache.Set(key, true, ResendCooldown);

            await _emailSender.SendEmailAsync(user.Email!, "Your RealEstateMarketplace password reset code",
                $"<p>Hello {HtmlEncoder.Default.Encode(user.FirstName)},</p>" +
                $"<p>Your password reset code is:</p><p style='font-size:28px;font-weight:bold;letter-spacing:6px'>{code}</p>" +
                "<p>The code expires in a few minutes. If you did not ask to reset your password, ignore this email.</p>");
            return true;
        }

        private const int MaxResetFailures = 5;
        private static readonly TimeSpan ResetFailureWindow = TimeSpan.FromMinutes(15);

        /// <summary>
        /// Checks the reset code and sets the new password. Wrong codes are
        /// counted separately from login failures (5 per 15 minutes), so someone
        /// who only knows the email cannot lock the owner out of logging in.
        /// Changing the password also changes the security stamp, so the same
        /// code cannot be used twice.
        /// </summary>
        public async Task<(VerifyResult Result, IEnumerable<IdentityError> Errors)> ResetPasswordAsync(
            ApplicationUser user, string code, string newPassword)
        {
            var failKey = "reset-code-failures:" + user.Id;
            var failures = _cache.TryGetValue(failKey, out int f) ? f : 0;
            if (failures >= MaxResetFailures || await _userManager.IsLockedOutAsync(user))
                return (VerifyResult.LockedOut, Array.Empty<IdentityError>());

            code = new string((code ?? "").Where(char.IsDigit).ToArray());
            bool ok = code.Length == 6 &&
                      await _userManager.VerifyUserTokenAsync(user, TokenOptions.DefaultEmailProvider, ResetPurpose, code);

            if (!ok)
            {
                failures++;
                _cache.Set(failKey, failures, ResetFailureWindow);
                return (failures >= MaxResetFailures ? VerifyResult.LockedOut : VerifyResult.WrongCode, Array.Empty<IdentityError>());
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
            if (!result.Succeeded) return (VerifyResult.WrongCode, result.Errors);

            _cache.Remove(failKey);
            await _userManager.ResetAccessFailedCountAsync(user);
            return (VerifyResult.Verified, Array.Empty<IdentityError>());
        }
    }

    public enum VerifyResult { Verified, WrongCode, LockedOut }
}
