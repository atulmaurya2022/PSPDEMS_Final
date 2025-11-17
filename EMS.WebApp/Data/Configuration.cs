using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMS.WebApp.Data
{
    [Table("Configuration")]
    public class Configuration
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int UID { get; set; }
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
