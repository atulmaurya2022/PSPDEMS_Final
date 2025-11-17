using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EMS.WebApp.Data;
using System;
using System.Runtime.InteropServices;

namespace EMS.WebApp.Controllers
{
    public class EmailConfigController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IEmailService _email;

        public EmailConfigController(ApplicationDbContext db, IEmailService email)
        {
            _db = db;
            _email = email;
        }

        [HttpGet]
        public IActionResult Index() => View();

        // ===== EMAIL TEMPLATE (BODY) =====

        // POST: EmailConfig/GetEmailBodyContent
        [HttpPost]
        public async Task<IActionResult> GetEmailBodyContent([FromForm(Name = "Type")] string type)
        {
            var subject = "";
            var body = "";

            if (!string.IsNullOrWhiteSpace(type))
            {
                var rec = await _db.EmailBodyConfigurations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Type != null && x.Type.Trim() == type.Trim());

                if (rec != null)
                {
                    subject = rec.Subject ?? "";
                    body = rec.EmailBody ?? "";
                }
            }

            // legacy shape: [subject, body]
            return Json(new[] { subject, body });
        }

        // POST: EmailConfig/SaveEmailContent
        
        [HttpPost]
        public async Task<IActionResult> SaveEmailContent(
            [FromForm(Name = "Type")] string type,
            [FromForm(Name = "Subject")] string subject,
            [FromForm(Name = "content")] string content)
        {
            if (string.IsNullOrWhiteSpace(type))
                return Json("Type is required.");

            var rec = await _db.EmailBodyConfigurations
                .FirstOrDefaultAsync(x => x.Type != null && x.Type.Trim() == type.Trim());

            if (rec == null)
                return Json("Unable to save Email Body Configuration !. Kindly contact System Admin");

            rec.Subject = subject?.Trim() ?? "";
            rec.EmailBody = content ?? "";

            await _db.SaveChangesAsync();
            return Json("success");
        }

        // POST: EmailConfig/GetEmailDetails
        [HttpGet]
        public IActionResult GetEmailDetails()
        {
            var list = _db.EmailBodyConfigurations
                .AsNoTracking()
                .Select(e => new EmailDetailsDto
                {
                    Type = e.Type ?? "",
                    Subject = e.Subject ?? "",
                    EmailBody = e.EmailBody ?? ""
                })
                .ToList();

            return Json(list);
        }

        // POST: EmailConfig/SendTestMail
        [HttpPost]
        public async Task<IActionResult> SendTestMail([FromForm] string type, [FromForm] string to, [FromForm] string cc)
        {
            var subject = "";
            var body = "";

            if (!string.IsNullOrWhiteSpace(type))
            {
                var rec = await _db.EmailBodyConfigurations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Type != null && x.Type.Trim() == type.Trim());
                if (rec != null)
                {
                    subject = rec.Subject ?? "";
                    body = rec.EmailBody ?? "";
                }
            }

            var toList = SplitEmails(to);
            var ccList = SplitEmails(cc);

            var status = await _email.SendTestAsync(toList, ccList, subject, body);
            return Json(status);
        }

        // ===== EMAIL CONFIG (SMTP) =====

        // POST: EmailConfig/GetEmailConfig
        [HttpGet]
        public IActionResult GetEmailConfig()
        {
            // legacy pattern: single row config (take first)
            var cfg = _db.Configurations.AsNoTracking().OrderBy(x => x.UID).FirstOrDefault();
            
            return Json(cfg);
        }

        // POST: EmailConfig/SaveEmailConfig
        
        [HttpPost]
        public async Task<IActionResult> SaveEmailConfig([FromForm] EmailSmtpDto model)
        {
            // Get/ensure single config row (first row pattern)
            var cfg = await _db.Configurations.OrderBy(x => x.UID).FirstOrDefaultAsync();
            if (cfg == null)
            {
                cfg = new Data.Configuration();
                _db.Configurations.Add(cfg);
            }

            cfg.Config_AppLink = model.Config_AppLink?.Trim();
            cfg.Config_SmtpHost = model.Config_SmtpHost?.Trim();
            cfg.Config_SmtpPort = model.Config_SmtpPort?.Trim();
            cfg.Config_SmtpUsername = model.Config_SmtpUsername?.Trim();
            cfg.Config_SmtpPassword = model.Config_SmtpPassword; // keep as-is (may be empty)
            cfg.Config_SmtpDisplayName = model.Config_SmtpDisplayName?.Trim();


            cfg.Reminder1Days = model.Reminder1Days;
            cfg.Reminder2Days = model.Reminder2Days;
            cfg.Reminder3Days = model.Reminder3Days;
            cfg.ScheduleHours = model.ScheduleHours;
            cfg.ScheduleMinutes = model.ScheduleMinutes;

            await _db.SaveChangesAsync();
            return Json("success");
        }

        private static IEnumerable<string> SplitEmails(string s) =>
            string.IsNullOrWhiteSpace(s)
                ? Enumerable.Empty<string>()
                : s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);


        [HttpPost]
        public IActionResult SendEmail(string emailType)
        {
            // If emailType is empty, fetch it from the EmailBodyConfiguration table based on the type
            if (string.IsNullOrEmpty(emailType))
            {
                // Return error if emailType is not provided
                return Json(new { success = false, message = "Email type is required." });
            }

            // Get the medical exam category details from the database using the cat_id (hardcoded for now, replace with dynamic logic)

            int catId = 1; // This should be passed dynamically or retrieved based on context

            var medicalExamCategory = _db.med_exam_categories
                                                .Where(x => x.CatId == catId)
                                                .FirstOrDefault();

            if (medicalExamCategory == null)
            {
                return Json(new { success = false, message = "Medical Exam Category not found." });
            }

            // Retrieve Application Link from the Configuration table
            var appLink = _db.Configurations
                                    .Select(c => c.Config_AppLink)
                                    .FirstOrDefault();

            // Check which type of email is being sent and fetch the content from EmailBodyConfiguration
            string subject = string.Empty;
            string body = string.Empty;

            var emailConfig = _db.EmailBodyConfigurations
                                        .Where(x => x.Type == emailType)
                                        .FirstOrDefault();

            if (emailConfig != null)
            {
                subject = emailConfig.Subject;
                body = emailConfig.EmailBody;
            }
            else
            {
                // Handle case where no configuration exists for the provided emailType
                return Json(new { success = false, message = "Email body configuration not found for the provided email type." });
            }

            // Replace the placeholders in the subject and body with actual values
            subject = subject.Replace("$$CategoryName$$", medicalExamCategory.CatName)
                             .Replace("$$ScheduledMonth$$", medicalExamCategory.MonthsSched)
                             .Replace("$$AnualRule$$", medicalExamCategory.AnnuallyRule)
                             .Replace("$$YearFrequency$$", medicalExamCategory.YearsFreq.ToString())
                             .Replace("$$ApplicationLink$$", appLink);

            body = body.Replace("$$CategoryName$$", medicalExamCategory.CatName)
                             .Replace("$$ScheduledMonth$$", medicalExamCategory.MonthsSched)
                             .Replace("$$AnualRule$$", medicalExamCategory.AnnuallyRule)
                             .Replace("$$YearFrequency$$", medicalExamCategory.YearsFreq.ToString())
                             .Replace("$$ApplicationLink$$", appLink);

            // Send the email (using a custom email service)
            List<string> toEmail = new List<string>();
            List<string> ccEmail = new List<string>();

            toEmail.Add("vanitha@getai.in");

            ccEmail.Add("vinod@getai.in");

            try
            {
                var status = _email.SendTestAsync(toEmail, ccEmail, subject, body);
                return Json(new { success = true, message = "Email sent successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Failed to send email: " + ex.Message });
            }
        }

    }

    public class EmailDetailsDto
    {
        public string Type { get; set; } = "";
        public string Subject { get; set; } = "";
        public string EmailBody { get; set; } = "";
    }

    public class EmailSmtpDto
    {
        public string? Config_AppLink { get; set; }
        public string? Config_SmtpHost { get; set; }
        public string? Config_SmtpPort { get; set; }
        public string? Config_SmtpUsername { get; set; }
        public string? Config_SmtpPassword { get; set; }
        public string? Config_SmtpDisplayName { get; set; }

        public int? Reminder1Days { get; set; }
        public int? Reminder2Days { get; set; }
        public int? Reminder3Days { get; set; }
        public int? ScheduleHours { get; set; }
        public int? ScheduleMinutes { get; set; }
    }
}
