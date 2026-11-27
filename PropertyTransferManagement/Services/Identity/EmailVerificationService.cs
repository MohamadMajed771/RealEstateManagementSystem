using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Caching.Memory;
using PropertyTransferManagement.Models;

namespace PropertyTransferManagement.Services.Identity
{
    /// <summary>
    /// 6-digit email verification codes (spec §20), the same mechanism as the
    /// Marketplace: Identity's email TOTP provider, valid for a few minutes.
    /// Wrong codes count as failed access attempts, so lockout stops guessing.
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

            var hello = string.IsNullOrWhiteSpace(user.FirstName) ? "Hello," : $"Hello {HtmlEncoder.Default.Encode(user.FirstName)},";
            await _emailSender.SendEmailAsync(user.Email!, "Your Transfer Service verification code",
                $"<p>{hello}</p>" +
                $"<p>Your verification code is:</p><p style='font-size:28px;font-weight:bold;letter-spacing:6px'>{code}</p>" +
                "<p>The code expires in a few minutes. If you did not create an account, ignore this email.</p>");
            return true;
        }

        public async Task<VerifyResult> VerifyAsync(ApplicationUser user, string? code)
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
    }

    public enum VerifyResult { Verified, WrongCode, LockedOut }
}
