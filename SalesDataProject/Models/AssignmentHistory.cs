using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SalesDataProject.Models
{
    [Table("TBL_ASSIGNMENT_HISTORY")]
    public class AssignmentHistory
    {
        [Key]
        public int ID { get; set; }

        [Required]
        [StringLength(255)]
        public string COMPANY_NAME { get; set; }

        [Required]
        [StringLength(255)]
        public string EMAIL_ID { get; set; }

        [Required]
        [StringLength(100)]
        public string ASSIGNED_TO { get; set; }

        [Required]
        [StringLength(100)]
        public string ASSIGNED_BY { get; set; }

        [Required]
        [StringLength(100)]
        public string CREATED_BY { get; set; }

        [Required]
        public DateTime ASSIGNED_ON { get; set; } = DateTime.UtcNow;
    }

}
