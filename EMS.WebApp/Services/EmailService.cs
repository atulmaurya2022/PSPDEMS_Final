// File: Services/EmailService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EMS.WebApp.Services;
using EMS.WebApp.Data;           // <-- change to your DbContext namespace
// using PSPDEMS.EmailConfig.Data; // (if you placed DbContext there)

namespace EMS.WebApp.Services
{
    public class EmailService : IEmailService
    {
        private readonly ApplicationDbContext _db; // <-- change type if your DbContext differs

        public EmailService(ApplicationDbContext db) // <-- change type if your DbContext differs
        {
            _db = db;
        }

        public async Task<string> SendTestAsync(IEnumerable<string> to, IEnumerable<string> cc, string subject, string htmlBody)
        {
            var cfg = await _db.Configurations.AsNoTracking().OrderBy(x => x.UID).FirstOrDefaultAsync();
            if (cfg == null) return "SMTP configuration not found.";

            if (string.IsNullOrWhiteSpace(cfg.Config_SmtpHost)) return "SMTP Host is empty.";
            if (!int.TryParse(cfg.Config_SmtpPort, out var port)) return "Invalid SMTP Port.";

            try
            {
                using var client = new SmtpClient(cfg.Config_SmtpHost, port)
                {
                    EnableSsl = port == 465 || port == 587 || port == 2525,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Credentials = string.IsNullOrWhiteSpace(cfg.Config_SmtpUsername)
                        ? CredentialCache.DefaultNetworkCredentials
                        : new NetworkCredential(cfg.Config_SmtpUsername, cfg.Config_SmtpPassword ?? string.Empty)
                };

                using var msg = new MailMessage
                {
                    From = new MailAddress(
                        string.IsNullOrWhiteSpace(cfg.Config_SmtpUsername) ? "no-reply@localhost" : cfg.Config_SmtpUsername,
                        string.IsNullOrWhiteSpace(cfg.Config_SmtpDisplayName) ? "System" : cfg.Config_SmtpDisplayName
                    ),
                    Subject = subject ?? string.Empty,
                    Body = htmlBody ?? string.Empty,
                    IsBodyHtml = true
                };

                foreach (var t in to.Where(x => !string.IsNullOrWhiteSpace(x))) msg.To.Add(t.Trim());
                foreach (var c in cc.Where(x => !string.IsNullOrWhiteSpace(x))) msg.CC.Add(c.Trim());

                await client.SendMailAsync(msg);
                return "success";
            }
            catch (Exception ex)
            {
                return $"SMTP send failed: {ex.Message}";
            }
        }
    }
}
