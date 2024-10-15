using System.ComponentModel.DataAnnotations;

namespace SalesDataProject.Models
{
    public class NewCustomer
    {
        public int ID { get; set; }

        [Required]
        public string CUSTOMER_CODE { get; set; }
        public string CUSTOMER_NAME { get; set; }
        public string CUSTOMER_EMAIL { get; set; }
        public string CUSTOMER_CONTACT_NUMBER { get; set; }
        public string COUNTRY { get; set; }
        public string CITY { get; set; }
        public string STATE { get; set; }
        public DateTime CREATED_ON { get; set; }
    }
}
