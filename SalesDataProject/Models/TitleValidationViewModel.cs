using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SalesDataProject.Models
{
    [Table("TBL_TITLES")]
    public class TitleValidationViewModel
    {
        [Key] // This makes it the primary key
        public int Id { get; set; } // Add a unique identifier
        public int RowNumber { get; set; } // Row number in the file
        public string CodeReference  { get; set; }
        public string InvoiceNumber { get; set; }
        public string Title { get; set; }
        public string CREATED_BY { get; set; }
        public DateOnly CREATED_ON { get; set; } 
        public string Status { get; set; } // "Blocked" or "Clean"
    }
}
