using System.ComponentModel.DataAnnotations.Schema;

namespace SalesDataProject.Models
{
    [Table("Countries")]
    public class Country
    {
        public int CountryId { get; set; }
        public string CountryName { get; set; }
        public string CountryCode { get; set; }
    }
}
