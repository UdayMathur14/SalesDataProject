namespace SalesDataProject.Models
{
    public class InvalidCustomerRecord
    {
        public int RowNumber { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerEmail { get; set; }
        public string? CustomerNumber { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
