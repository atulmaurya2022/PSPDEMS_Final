
using EMS.WebApp.Data;
using EMS.WebApp.Services;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace EMS.WebApp.Controllers
{

    public interface IMedCheckReminderService
    {
        Task RunAsync(DateTime now, CancellationToken ct = default);
    }

    public sealed class MedCheckReminderService : IMedCheckReminderService
    {
        private readonly ApplicationDbContext _db;
        private readonly IEmailService _email;

        public MedCheckReminderService(ApplicationDbContext db, IEmailService email)
        {
            _db = db;
            _email = email;
        }

        public async Task RunAsync(DateTime now, CancellationToken ct = default)
        {
            // 1. Get reminder day defaults from Configuration
            var configValues =  _db.Configurations.FirstOrDefault();
            int rem1 = (int)(configValues?.Reminder1Days > 0 ? configValues.Reminder1Days : 30); // default 1 month before
            int rem2 = (int)(configValues?.Reminder2Days > 0 ? configValues.Reminder2Days : 15); // default 15 days
            int rem3 = (int)(configValues?.Reminder3Days > 0 ? configValues.Reminder3Days : 1);  // default 1 day before

            // 2. Loop through all med_exam_category
            var categories =  _db.med_exam_categories
                .OrderBy(c => c.CatName)
                .ToList();

            foreach (var category in categories)
            {
                var plantIds =_db.MedExaminationResults
                            .Where(r => r.CatId == category.CatId && r.PlantId != null) // filter nulls
                            .Select(r => r.PlantId!.Value)                               // safe because we filtered
                            .Distinct()
                            .ToList();

                foreach (var plantId in plantIds)
                {
                    var plantdetails = _db.org_plants
                                .Where(e => e.plant_id == plantId)
                                .FirstOrDefault();

                    string PlantName = "";
                    string PlantCode =  "";

                    if (plantdetails != null)
                    {
                        PlantName = plantdetails.plant_name ?? "";
                        PlantCode = plantdetails.plant_code ?? "";
                    }

                    // 4. Get all active doctors for this plant
                    var doctorRoleId =  _db.SysRoles
                        .Where(r => r.role_name == "Doctor")
                        .Select(r => r.role_id)
                        .FirstOrDefault();

                    var doctors =  _db.SysUsers
                        .Where(u => u.is_active && u.role_id == doctorRoleId && u.plant_id == plantId)
                        .ToList();

                    var toEmails = doctors.Select(d => d.email).ToList();

                    if (!toEmails.Any())
                        continue; // skip if no doctors

                    // 5. Parse months_sched to determine the reminder dates
                    var scheduledMonths = category.MonthsSched.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                                .Select(m => m.Trim())
                                                .ToList();

                    var formats = new[] { "MMMM yyyy", "MMM yyyy" }; // October / Oct
                    
                    foreach (var month in scheduledMonths)
                    {
                        // --- pick the occurrence (this year or next) whose reminder window hits today ---
                        DateTime? selectedDue = null;
                        List<DateTime>? selectedReminders = null;
                        int hitIdx = -1;

                        foreach (var yr in new[] { DateTime.Today.Year, DateTime.Today.Year + 1 })
                        {
                            if (!DateTime.TryParseExact($"{month} {yr}",
                                                        formats,
                                                        CultureInfo.InvariantCulture,
                                                        DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal,
                                                        out var dueDateForYear))
                            {
                                continue;
                            }

                            var remindersForYear = new List<DateTime>
                            {
                                dueDateForYear.AddDays(-rem1).Date,
                                dueDateForYear.AddDays(-rem2).Date,
                                dueDateForYear.AddDays(-rem3).Date
                            };

                            var idx = remindersForYear.FindIndex(d => d == DateTime.Today);
                            if (idx >= 0)
                            {
                                selectedDue = dueDateForYear;
                                selectedReminders = remindersForYear;
                                hitIdx = idx;
                                break; // we found the occurrence whose reminder is today (e.g., Jan next year -> Dec this year)
                            }
                        }

                        // If today doesn't match any reminder for this month (this year or next), skip
                        if (hitIdx < 0 || selectedDue == null || selectedReminders == null)
                            continue;

                        // Label the reminder (1/2/3) and name (First/Second/Third)
                        string[] names = { "First", "Second", "Third" };
                        string remindernumber = (hitIdx + 1).ToString();
                        string remindername = names[hitIdx];

                        var today = DateTime.Today;
                        // Check if today matches any reminder date
                        foreach (var reminderDate in selectedReminders)
                        {
                            if (reminderDate != today)
                                continue;

                            // 6. Prepare email content dynamically
                            var emailBodyConfig = _db.EmailBodyConfigurations
                                .Where(e => e.Type == "MedicalCheck_Pending") // or dynamic type if you want
                                .FirstOrDefault();

                            if (emailBodyConfig == null)
                                continue;

                            string subject = emailBodyConfig.Subject;
                            string body = emailBodyConfig.EmailBody;

                            
                            // Replace the placeholders in the subject and body with actual values
                            subject = subject.Replace("$$CategoryName$$", category.CatName)
                                             .Replace("$$ScheduledMonth$$", month)
                                             .Replace("$$PlantName$$", PlantName)
                                             .Replace("$$PlantCode$$", PlantCode)
                                             .Replace("$$AnualRule$$", category.AnnuallyRule)
                                             .Replace("$$YearFrequency$$", category.YearsFreq.ToString())
                                             .Replace("$$ApplicationLink$$", configValues?.Config_AppLink ?? "")
                                             .Replace("$$ReminderNumber$$", remindernumber);

                            body = body.Replace("$$CategoryName$$", category.CatName)
                                             .Replace("$$ScheduledMonth$$", month)
                                             .Replace("$$PlantName$$", PlantName)
                                             .Replace("$$PlantCode$$", PlantCode)
                                             .Replace("$$AnualRule$$", category.AnnuallyRule)
                                             .Replace("$$YearFrequency$$", category.YearsFreq.ToString())
                                             .Replace("$$ApplicationLink$$", configValues?.Config_AppLink ?? "")
                                             .Replace("$$ReminderNumber$$", remindernumber);
                            List<string> ccEmail = new List<string>();

                            await _email.SendTestAsync(
                                toEmails,
                                ccEmail, // cc emails, can extend later
                                subject,
                                body
                            );
                        }
                    }
                }
            }
        }
    }


    
}
