using SalesDataProject.Models.SalesDataProject.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SalesDataProject.Models
{
    [Table("TBL_PROSPECT_CUSTOMER_BLOCKED")]
    public class ProspectCustomerBlocked : ProspectCustomerBase
    {
        public int ID { get; set; } // Primary Key, auto-incremented
        public string? BLOCK_REASON { get; set; }
        public string? RELEASED { get; set; }
        public string? RELEASED_BY { get; set; }
        public string? RELEASED_ON { get; set; }
        public string? BLOCKED_BY { get; set; }


        public string? CREATED_BY { get; set; } // Created by information
        public DateTime? CREATED_ON { get; set; } = DateTime.UtcNow;// Creation timestamp

    }
}
