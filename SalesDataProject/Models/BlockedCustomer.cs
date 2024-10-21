using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SalesDataProject.Models
{
    [Table("TBL_BLOCKED_CUSTOMERS")]
    public class BlockedCustomer
    {
        [Key]
        public int Id { get; set; }
        public string CUSTOMER_CODE { get; set; }

        [StringLength(100)]
        public string CUSTOMER_NAME { get; set; }

        [StringLength(100)]
        [EmailAddress]
        public string CUSTOMER_EMAIL { get; set; }

        [StringLength(15)]
        public string CUSTOMER_CONTACT_NUMBER1 { get; set; }

        [StringLength(50)]
        public string COUNTRY { get; set; }

        [StringLength(50)]
        public string CITY { get; set; }

        [StringLength(50)]
        public string STATE { get; set; }

        public string CREATED_BY { get; set; } // Created by information
        public DateTime CREATED_ON { get; set; } = DateTime.UtcNow;// Creation timestamp

        public string MODIFIED_BY { get; set; } // Modified by information
        public DateTime? MODIFIED_ON { get; set; } = DateTime.UtcNow; // Nullable in case it hasn't been modified yet

        [Required]
        public DateTime BlockedDate { get; set; }
    }
}

