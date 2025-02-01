namespace SalesDataProject.Models
{
    public class UploadResultViewModel
    {
        public List<ProspectCustomer>? BlockedCustomers { get; set; } = new List<ProspectCustomer>();
        public List<ProspectCustomer>? CleanCustomers { get; set; } = new List<ProspectCustomer>();
        public DateTime? SelectedDate { get; set; } 
        public string? RecordType { get; set; } // Can be "Blocked" or "Clean"
        public List<ProspectCustomer>? CleanCustomersEmailList { get; set; } = new List<ProspectCustomer>();
        public List<ProspectCustomer>? BlockCustomersEmailList { get; set; } = new List<ProspectCustomer>();//to block and for unblock
        public List<InvalidCustomerRecord>? invalidCustomerRecords { get; set; } = new List<InvalidCustomerRecord>();

        public string? Category { get; set; }

        public string? UserName { get; set; }
        public string? Event { get; set; }

    }
}
