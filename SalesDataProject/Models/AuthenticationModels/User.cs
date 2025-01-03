using System.ComponentModel.DataAnnotations;

namespace SalesDataProject.Models.AuthenticationModels
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        public string Username { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [StringLength(20, MinimumLength = 6, ErrorMessage = "Password must be of 6 characters")]
        public string Password { get; set; }

        public bool CanAccessCustomer { get; set; } = false;

        public bool CanAccessSales { get; set; } = false;

        public bool CanAccessUserManagement{ get; set; } = false;
        public bool CanAccessTitle { get; set; } = false;
        public bool CanViewTitles { get; set; } = false;
    }
}
