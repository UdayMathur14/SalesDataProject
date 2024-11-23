namespace SalesDataProject.Models
{
    public class UploadResultViewModel
    {
        public List<ProspectCustomer>? BlockedCustomers { get; set; }
        public List<ProspectCustomer>? CleanCustomers { get; set; }
        public DateTime? SelectedDate { get; set; } 
        public string? RecordType { get; set; } // Can be "Blocked" or "Clean"
        public List<ProspectCustomer>? CleanCustomersEmailList { get; set; }
        public List<ProspectCustomer>? BlockCustomersEmailList { get; set; }//to block and for unblock
        public List<InvalidCustomerRecord>? invalidCustomerRecords { get; set; }

        public string? Category { get; set; }

    }
}
