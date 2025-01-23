using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SalesDataProject.Models
{
    [Table("TBL_PROSPECT_CUSTOMER")]
    public class ProspectCustomer
    {
        private static readonly HashSet<string> CommonDomains = new HashSet<string>
        {
             
        };
        public int ID { get; set; } // Primary Key, auto-incremented
        public int SALES_PERSON_ID { get; set; } = 0001; 

        [Required]
        [StringLength(50)]
        public string? CUSTOMER_CODE { get; set; } // Unique, not null

        [StringLength(100)]
        public string COMPANY_NAME { get; set; }

        [StringLength(100)]
        public string CONTACT_PERSON { get; set; }

        [RegularExpression(@"^\d+$", ErrorMessage = "Please enter only numeric values.")]
        public string CUSTOMER_CONTACT_NUMBER1 { get; set; }


        [RegularExpression(@"^\d+$", ErrorMessage = "Please enter only numeric values.")]
        public string? CUSTOMER_CONTACT_NUMBER2 { get; set; }


        [RegularExpression(@"^\d+$", ErrorMessage = "Please enter only numeric values.")]
        public string? CUSTOMER_CONTACT_NUMBER3 { get; set; }


        [Required]
        [StringLength(100)]
        [EmailAddress]
        public string CUSTOMER_EMAIL { get; set; }

        [StringLength(50)]
        public string COUNTRY { get; set; }

        [StringLength(50)]
        public string? CITY { get; set; }

        [StringLength(50)]
        public string? STATE { get; set; }

        public string? CREATED_BY { get; set; } // Created by information
        public DateTime? CREATED_ON { get; set; } = DateTime.UtcNow;// Creation timestamp

        public string? MODIFIED_BY { get; set; } // Modified by information
        public DateTime? MODIFIED_ON { get; set; } = DateTime.UtcNow; // Nullable in case it hasn't been modified yet

        public bool RECORD_TYPE { get; set; } = false;
        public bool IS_EMAIL_BLOCKED { get; set; } = false;

        public string CATEGORY { get; set; }

        public string COUNTRY_CODE { get; set; }

        public string? BLOCKED_BY { get; set; }

        private string? _emailDomain;
        public string? EMAIL_DOMAIN
        {
            get => _emailDomain;
            set
            {
                var domain = value?.Split('@').Last().ToLower();
                _emailDomain = CommonDomains.Contains(domain) ? null : domain;
            }
        }

    }
}
