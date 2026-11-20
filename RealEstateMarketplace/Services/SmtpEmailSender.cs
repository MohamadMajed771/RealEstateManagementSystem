
using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace RealEstateMarketplace.Services
{
    public class SmtpEmailSender : IEmailSender
    {
        private readonly IConfiguration _configuration;

        public SmtpEmailSender(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(
            string email,
            string subject,
            string htmlMessage)
        {
            Console.WriteLine($"SMTP EMAIL SENDER CALLED. Recipient: {email}");
            var host = _configuration["SmtpSettings:Host"]
                ?? throw new InvalidOperationException(
                    "SMTP host is missing.");

            var username = _configuration["SmtpSettings:Username"]
                ?? throw new InvalidOperationException(
                    "SMTP username is missing.");

            var password = _configuration["SmtpSettings:Password"]
                ?? throw new InvalidOperationException(
                    "SMTP password is missing.");

            var fromEmail = _configuration["SmtpSettings:FromEmail"]
                ?? throw new InvalidOperationException(
                    "SMTP sender email is missing.");

            var fromName =
                _configuration["SmtpSettings:FromName"]
                ?? "RealEstateMarketplace";

            var port = _configuration.GetValue<int>(
                "SmtpSettings:Port", 587);

            using var smtpClient = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(
                    username, password),

                EnableSsl = true,

                DeliveryMethod = SmtpDeliveryMethod.Network,

                Timeout = 15000
            };

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(fromEmail, fromName),
                Subject = subject,
                Body = htmlMessage,
                IsBodyHtml = true
            };

            mailMessage.To.Add(email);

            try
            {
                Console.WriteLine("SMTP: Attempting to send email...");

                await smtpClient.SendMailAsync(mailMessage);

                Console.WriteLine("SMTP: Email accepted by SMTP server.");
            }
            catch (SmtpException ex)
            {
                Console.WriteLine("SMTP ERROR: " + ex.Message);
                Console.WriteLine("SMTP STATUS: " + ex.StatusCode);

                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine("EMAIL ERROR: " + ex.Message);

                throw;
            }
        }
    }
}
