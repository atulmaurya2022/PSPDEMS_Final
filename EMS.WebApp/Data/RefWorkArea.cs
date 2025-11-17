using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;


namespace EMS.WebApp.Data
{

    [Table("ref_work_area")]

    public class RefWorkArea

    {

        [Key]

        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]

        [Column("area_uid")]

        public int area_uid { get; set; }

        [Column("area_code")]

        [StringLength(40)]

        [Display(Name = "Area Code")]

        public string? area_code { get; set; }

        [Column("area_desc")]

        [StringLength(40)]

        [Display(Name = "Area Description")]

        public string? area_desc { get; set; }

        // Navigation properties

  
        public ICollection<MedExamWorkArea> MedExamWorkAreas { get; set; } = new List<MedExamWorkArea>();

    }
}
