using System.ComponentModel.DataAnnotations.Schema;

namespace SalesDataProject.Models
{
    [Table("TBL_COMMON_DOMAINS")]
    public class CommonDomains
    {
        public int Id { get; set; }
        public string DomainName { get; set; }
    }
}
