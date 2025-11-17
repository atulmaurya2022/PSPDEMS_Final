using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMS.WebApp.Data
{
    [Table("EmailBodyConfiguration")]
    public class EmailBodyConfiguration
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int UID { get; set; }
        public string? Type { get; set; }
        public string? Subject { get; set; }
        public string? EmailBody { get; set; }
    }
}
