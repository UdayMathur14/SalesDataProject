using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SalesDataProject.Models
{
    [Table("TBL_EXISTING_CUSTOMER")]
    public class Customer
    {
        public int ID { get; set; } // Primary Key, auto-incremented

        [Required]
        [StringLength(50)]
        public string CUSTOMER_CODE { get; set; } // Unique, not null

        [StringLength(100)]
        public string CUSTOMER_NAME { get; set; }

        [StringLength(100)]
        [EmailAddress]
        public string CUSTOMER_EMAIL { get; set; }

        [StringLength(15)]
        public string CUSTOMER_CONTACT_NUMBER { get; set; }

        [StringLength(50)]
        public string COUNTRY { get; set; }

        [StringLength(50)]
        public string? CITY { get; set; }

        [StringLength(50)]
        public string STATE { get; set; }

        public string CREATED_BY { get; set; } // Created by information
        public DateTime CREATED_ON { get; set; } = DateTime.UtcNow;// Creation timestamp

        public string MODIFIED_BY { get; set; } // Modified by information
        public DateTime? MODIFIED_ON { get; set; } =DateTime.UtcNow; // Nullable in case it hasn't been modified yet
    }
}
