using System.ComponentModel.DataAnnotations;

namespace SalesDataProject.Models
{
    public class AssignToViewModel
    {
        // Selected user for assignment
        [Required(ErrorMessage = "Please select a user.")]
        public string UserName { get; set; }

        // Selected category to filter records
        public string Category { get; set; }

        // List of records to display on the page
        public List<ProspectCustomerClean> RecordsList { get; set; }
        public List<AssignmentHistory> AssignmentHistoryList { get; set; }

        public AssignToViewModel()
        {
            RecordsList = new List<ProspectCustomerClean>();
            AssignmentHistoryList = new List<AssignmentHistory>();
        }
    }
}
