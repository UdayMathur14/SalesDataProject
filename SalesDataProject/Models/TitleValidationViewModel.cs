using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SalesDataProject.Models
{
    [Table("TBL_TITLES")]
    public class TitleValidationViewModel
    {
        [Key]
        public int Id { get; set; }

        public int RowNumber { get; set; }

        public string? CodeReference { get; set; }
        public string? InvoiceNumber { get; set; }
        public string? PaperId { get; set; }
        public string? Title { get; set; }
        public string? UpdatedTitle { get; set; }
        public string? CREATED_BY { get; set; }
        public DateOnly CREATED_ON { get; set; }
        public string? Status { get; set; }
        public string? ReferenceTitle { get; set; }
        public string? UpdatedReferenceTitle { get; set; }
        public string? UpdatedTitleBy { get; set; }
        public string? TitleYear { get; set; }

        // Existing blocked details
        [NotMapped]
        public int? BlockedId { get; set; }

        [NotMapped]
        public string? BlockedByInvoiceNo { get; set; }

        [NotMapped]
        public string? BlockedCodeRef { get; set; }

        // New fields for showing duplicate based on existing DB record
        [NotMapped]
        public string? BlockedByPaperId { get; set; }

        [NotMapped]
        public string? BlockedByTitle { get; set; }
    }
}
