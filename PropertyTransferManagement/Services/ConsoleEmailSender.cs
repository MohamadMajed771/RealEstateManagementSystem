using Microsoft.AspNetCore.Identity.UI.Services;

namespace PropertyTransferManagement.Services
{
    /// <summary>
    /// Development fallback used when no SmtpSettings are configured.
    /// The email (including verification / reset links) is written to
    /// the application console instead of being sent.
    /// </summary>
    public class ConsoleEmailSender : IEmailSender
    {
        private readonly ILogger<ConsoleEmailSender> _logger;

        public ConsoleEmailSender(ILogger<ConsoleEmailSender> logger)
        {
            _logger = logger;
        }

        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            _logger.LogWarning(
                "DEV EMAIL (SMTP not configured)\nTo: {Email}\nSubject: {Subject}\n{Body}",
                email, subject, System.Net.WebUtility.HtmlDecode(htmlMessage));

            return Task.CompletedTask;
        }
    }
}
