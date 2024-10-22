using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SalesDataProject.Models
{
    [Table("TBL_PROSPECT_CUSTOMER")]
    public class ProspectCustomer
    {
        public int ID { get; set; } // Primary Key, auto-incremented
        public int SALES_PERSON_ID { get; set; } = 0001; 

        [Required]
        [StringLength(50)]
        public string? CUSTOMER_CODE { get; set; } // Unique, not null

        [StringLength(100)]
        public string CUSTOMER_NAME { get; set; }

        [StringLength(100)]
        public string? CONTACT_PERSON { get; set; }
        [StringLength(7, MinimumLength = 7, ErrorMessage = "Contact number must be minimum 7 digits.")]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "Please Enter Correct Number")]
        public string CUSTOMER_CONTACT_NUMBER1 { get; set; }

        [StringLength(7, MinimumLength = 7, ErrorMessage = "Contact number must be minimum 7 digits.")]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "Please Enter Correct Number")]
        public string? CUSTOMER_CONTACT_NUMBER2 { get; set; }

        [StringLength(7, MinimumLength = 7, ErrorMessage = "Contact number must be minimum 7 digits.")]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "Please Enter Correct Number")]
        public string? CUSTOMER_CONTACT_NUMBER3 { get; set; }


        [StringLength(100)]
        [EmailAddress]
        public string? CUSTOMER_EMAIL { get; set; }

        [StringLength(50)]
        public string COUNTRY { get; set; }

        [StringLength(50)]
        public string CITY { get; set; }

        [StringLength(50)]
        public string STATE { get; set; }

        public string? CREATED_BY { get; set; } // Created by information
        public DateTime? CREATED_ON { get; set; } = DateTime.UtcNow;// Creation timestamp

        public string? MODIFIED_BY { get; set; } // Modified by information
        public DateTime? MODIFIED_ON { get; set; } = DateTime.UtcNow; // Nullable in case it hasn't been modified yet

        public bool RECORD_TYPE { get; set; } = false;
        public bool IS_EMAIL_BLOCKED { get; set; } = false;
    }
}
