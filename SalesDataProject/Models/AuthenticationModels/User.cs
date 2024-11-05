using System.ComponentModel.DataAnnotations;

namespace SalesDataProject.Models.AuthenticationModels
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        public string Username { get; set; }

        [Required]
        [StringLength(10, MinimumLength = 10, ErrorMessage = "Passport must be of 6 characters")]
        public string Password { get; set; }

        public bool CanAccessCustomer { get; set; } = false;

        public bool CanAccessSales { get; set; } = false;

        public bool CanAccessUserManagement{ get; set; } = false;
    }
}
