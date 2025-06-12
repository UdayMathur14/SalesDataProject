using SalesDataProject.Models.SalesDataProject.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SalesDataProject.Models
{
    [Table("TBL_PROSPECT_CUSTOMER_CLEAN")]
    public class ProspectCustomerClean : ProspectCustomerBase
    {
        
        public int ID { get; set; } // Primary Key, auto-incremented
        public int SALES_PERSON_ID { get; set; } = 0001; 

        public string? CREATED_BY { get; set; } // Created by information
        public DateTime? CREATED_ON { get; set; } = DateTime.UtcNow;// Creation timestamp

        public string? MODIFIED_BY { get; set; } // Modified by information
        public DateTime? MODIFIED_ON { get; set; } = DateTime.UtcNow; // Nullable in case it hasn't been modified yet

    }
}
