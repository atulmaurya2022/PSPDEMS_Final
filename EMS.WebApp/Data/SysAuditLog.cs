using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMS.WebApp.Data
{
    [Table("sys_audit_log")]
    public class SysAuditLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("audit_id")]
        public long AuditId { get; set; }

        [Required]
        [StringLength(50)]
        [Column("table_name")]
        [Display(Name = "Table Name")]
        public string TableName { get; set; } = null!;

        [Required]
        [StringLength(20)]
        [Column("action_type")]
        [Display(Name = "Action Type")]
        public string ActionType { get; set; } = null!; // CREATE, UPDATE, DELETE, VIEW

        [Column("record_id")]
        [Display(Name = "Record ID")]
        public string RecordId { get; set; } = null!; // Can be composite key as string

        [Column("old_values", TypeName = "text")]
        [Display(Name = "Old Values")]
        public string? OldValues { get; set; } // JSON format

        [Column("new_values", TypeName = "text")]
        [Display(Name = "New Values")]
        public string? NewValues { get; set; } // JSON format

        [Required]
        [StringLength(100)]
        [Column("user_name")]
        [Display(Name = "User Name")]
        public string UserName { get; set; } = null!;

        [StringLength(50)]
        [Column("user_id")]
        [Display(Name = "User ID")]
        public string? UserId { get; set; }

        [StringLength(45)]
        [Column("ip_address")]
        [Display(Name = "IP Address")]
        public string? IpAddress { get; set; }

        [StringLength(500)]
        [Column("user_agent")]
        [Display(Name = "User Agent")]
        public string? UserAgent { get; set; }

        [Column("timestamp")]
        [Display(Name = "Timestamp")]
        public DateTime Timestamp { get; set; }

        [StringLength(200)]
        [Column("controller_action")]
        [Display(Name = "Controller/Action")]
        public string? ControllerAction { get; set; }

        [Column("additional_info", TypeName = "text")]
        [Display(Name = "Additional Info")]
        public string? AdditionalInfo { get; set; }
    }
}
