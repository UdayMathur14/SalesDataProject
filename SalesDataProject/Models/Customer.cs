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

        [Required]
        [StringLength(100)]
        [EmailAddress]
        public string CUSTOMER_EMAIL { get; set; }

        [StringLength(10, MinimumLength = 10, ErrorMessage = "Contact number must be exactly 10 digits.")]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "Please Enter Correct Number")]
        public string CUSTOMER_CONTACT_NUMBER { get; set; }

        [StringLength(50)]
        public string COUNTRY { get; set; }

        [StringLength(50)]
        public string? CITY { get; set; }

        [StringLength(50)]
        public string STATE { get; set; }

        public string CREATED_BY { get; set; } // Created by information
        public DateTime CREATED_ON { get; set; } = DateTime.UtcNow;// Creation timestamp

        public string? MODIFIED_BY { get; set; } // Modified by information
        public DateTime? MODIFIED_ON { get; set; } =DateTime.UtcNow; // Nullable in case it hasn't been modified yet
    }
}
