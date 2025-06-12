namespace SalesDataProject.Models
{
    public class UploadResultViewModel
    {
        public List<ProspectCustomerBlocked>? BlockedCustomers { get; set; } = new List<ProspectCustomerBlocked>();
        public List<ProspectCustomerClean>? CleanCustomers { get; set; } = new List<ProspectCustomerClean>();
        public DateTime? SelectedDate { get; set; } 
        public string? RecordType { get; set; } // Can be "Blocked" or "Clean"
        public List<ProspectCustomerClean>? CleanCustomersEmailList { get; set; } = new List<ProspectCustomerClean>();
        public List<ProspectCustomerBlocked>? BlockCustomersEmailList { get; set; } = new List<ProspectCustomerBlocked>();//to block and for unblock
        public List<InvalidCustomerRecord>? invalidCustomerRecords { get; set; } = new List<InvalidCustomerRecord>();

        public string? Category { get; set; }

        public string? UserName { get; set; }
        public string? Event { get; set; }

    }
}
