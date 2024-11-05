// Controllers/AuthController.cs
using Microsoft.AspNetCore.Mvc;
using SalesDataProject.Models.AuthenticationModels;

namespace SalesDataProject.Controllers
{
    public class AuthController : Controller
    {
        private readonly AppDbContext _context;

        public AuthController(AppDbContext context)
        {
            _context = context;
        }

        // Login GET
        public IActionResult Login()
        {
            return View();
        }

        // Login POST
        [HttpPost]
        public IActionResult Login(User model)
        {
            var user = _context.Users.FirstOrDefault(u => u.Username == model.Username && u.Password == model.Password);
            if (user != null)
            {
                HttpContext.Session.SetInt32("UserId", user.Id);
                HttpContext.Session.SetString("CanAccessCustomer", user.CanAccessCustomer.ToString());
                HttpContext.Session.SetString("CanAccessSales", user.CanAccessSales.ToString());
                HttpContext.Session.SetString("CanAccessUserManagement", user.CanAccessUserManagement.ToString());
                TempData["Success"] = "Successfully Login";
                return RedirectToAction("Index", "Home");

            }

            TempData["Error"] = "Incorrect password. Please try again.";
            return View(model);
        }

        // User Management GET
        public IActionResult ManageUsers()
        {
            if (HttpContext.Session.GetString("CanAccessUserManagement") != "True")
            {
                // If not authorized, redirect to home or another page
                return RedirectToAction("Login", "Auth");
            }
            var users = _context.Users.ToList(); // Get all users
            return View(users);
        }

        // Create User GET
        public IActionResult CreateUser()
        {
            if (HttpContext.Session.GetString("CanAccessUserManagement") != "True")
            {
                // If not authorized, redirect to home or another page
                return RedirectToAction("Login", "Auth");
            }
            return View();
        }

        // Create User POST
        [HttpPost]
        public IActionResult CreateUser(User model)
        {
            if (ModelState.IsValid)
            {
                _context.Users.Add(model);
                _context.SaveChanges();
                return RedirectToAction("ManageUsers");
            }

            return View(model);
        }
        [HttpPost]
        public IActionResult UpdateUserAccess(List<User> Users)
        {
            foreach (var user in Users)
            {
                // Fetch the existing user from the database (replace with your data context)
                var existingUser = _context.Users.FirstOrDefault(u => u.Username == user.Username);
                if (existingUser != null)
                {
                    // Update the access permissions based on form data
                    existingUser.CanAccessCustomer = user.CanAccessCustomer;
                    existingUser.CanAccessSales = user.CanAccessSales;

                    // Save changes to the database
                    _context.Update(existingUser);
                }
            }

            // Commit all changes at once
            _context.SaveChanges();

            // Redirect or return a view after updating
            return RedirectToAction("ManageUsers");
        }

    }
}
