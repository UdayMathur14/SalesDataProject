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
                //HttpContext.Session.SetString("CanAccessCustomer", user.CanAccessCustomer.ToString());
                //HttpContext.Session.SetString("CanAccessSales", user.CanAccessSales.ToString());
                //HttpContext.Session.SetString("CanAccessUserManagement", user.CanAccessUserManagement.ToString());
                HttpContext.Session.SetString("CanAccessCustomer", user.CanAccessCustomer ? "true" : "false");
                HttpContext.Session.SetString("CanAccessSales", user.CanAccessSales ? "true" : "false");
                HttpContext.Session.SetString("CanAccessUserManagement", user.CanAccessUserManagement ? "true" : "false");
                return RedirectToAction("Index", "Home");

            }

            ModelState.AddModelError("", "Invalid login attempt.");
            return View(model);
        }

        // User Management GET
        public IActionResult ManageUsers()
        {
            if (HttpContext.Session.GetString("CanAccessUserManagement") != "true")
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
            if (HttpContext.Session.GetString("CanAccessUserManagement") != "true")
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
    }
}
