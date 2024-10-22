namespace SalesDataProject.Models
{
    public class UploadResultViewModel
    {
        public List<ProspectCustomer>? BlockedCustomers { get; set; }
        public List<ProspectCustomer>? CleanCustomers { get; set; }
        public List<BlockedCustomer>? blockCustomer { get; set; }
        public DateTime? SelectedDate { get; set; } 
        public string? RecordType { get; set; } // Can be "Blocked" or "Clean"
    }
}
