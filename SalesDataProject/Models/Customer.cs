using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SalesDataProject.Models
{

    [Table("TBL_EXISTING_CUSTOMER")]
    public class Customer
    {
        private static readonly HashSet<string> CommonDomains = new HashSet<string>
        {
            "gmail.com", "yahoo.com", "yahoo.co.in", "outlook.com", "hotmail.com"
        };

        public int ID { get; set; } // Primary Key, auto-incremented

        [StringLength(50)]
        [JsonProperty("customer_code")]
        public string? CUSTOMER_CODE { get; set; } // Unique, not null

        [Required]
        [StringLength(100)]
        [JsonProperty("company_name")]
        public string COMPANY_NAME { get; set; }

        [StringLength(100)]
        public string CONTACT_PERSON { get; set; }

        public string? CountryCode { get; set; }


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

        private string? _emailDomain;

        public string? EmailDomain
        {
            get => _emailDomain;
            set
            {
                var domain = value?.Split('@').Last().ToLower();
                _emailDomain = CommonDomains.Contains(domain) ? null : domain;
            }
        }

        [Required]
        public string Category { get; set; }
    }
}
