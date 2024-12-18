// Controllers/AuthController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SalesDataProject.Models;
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
        public async Task<IActionResult> AssignRecords()
        {
            var users = await _context.Users.ToListAsync();
            ViewBag.Users = new SelectList(users, "Username", "Username");

            var model = new AssignToViewModel
            {
                RecordsList = new List<ProspectCustomer>()
            };

            return View(model);
        }
        public async Task<IActionResult> ChangeRecordType(UploadResultViewModel model)
        {
            if (HttpContext.Session.GetString("CanAccessSales") != "True")
            {
                // If not authorized, redirect to home or another page
                return RedirectToAction("Login", "Auth");
            }
            var users = await _context.Users.ToListAsync();

            // Pass the list of users to the view using ViewBag
            ViewBag.Users = new SelectList(users, "Username", "Username");

            return View(model);

        }

        // Login POST
        [HttpPost]
        public IActionResult Login(User model)
        {
            var user = _context.Users.FirstOrDefault(u => u.Username == model.Username && u.Password == model.Password);
            if (user != null)
            {
                HttpContext.Session.SetInt32("UserId", user.Id);
                HttpContext.Session.SetString("Username", user.Username.ToString());
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
                TempData["success"] = "Succesfully Created";
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
                    existingUser.CanAccessUserManagement = user.CanAccessUserManagement;

                    // Save changes to the database
                    _context.Update(existingUser);
                }
            }

            // Commit all changes at once
            _context.SaveChanges();

            // Redirect or return a view after updating
            TempData["success"] = "Updated Succesfully";
            return RedirectToAction("ManageUsers");
        }


        [HttpPost]
        public async Task<IActionResult> UpdateCustomerStatus(List<int> BlockedCustomerIds, List<int> CleanCustomerIds)
        {
            if (BlockedCustomerIds.Count == 0 && CleanCustomerIds.Count == 0)
            {
                return RedirectToAction("ChangeRecordType");
            }
            // Change blocked customers to clean
            if (BlockedCustomerIds != null && BlockedCustomerIds.Any())
            {
                var blockedCustomers = await _context.Prospects
                    .Where(c => BlockedCustomerIds.Contains(c.ID))
                    .ToListAsync();

                foreach (var customer in blockedCustomers)
                {
                    customer.IS_EMAIL_BLOCKED = false; // Change to clean
                    customer.RECORD_TYPE = false;
                }

                await _context.SaveChangesAsync();
                TempData["messagesuccess"] = "Successfully cleaned selected companies.";
            }

            // Change clean customers to blocked
            if (CleanCustomerIds != null && CleanCustomerIds.Any())
            {
                var cleanCustomers = await _context.Prospects
                    .Where(c => CleanCustomerIds.Contains(c.ID))
                    .ToListAsync();

                foreach (var customer in cleanCustomers)
                {
                    customer.IS_EMAIL_BLOCKED = true; // Change to blocked
                    customer.RECORD_TYPE = true;
                }
                await _context.SaveChangesAsync();
                TempData["messagesuccess"] = "Successfully blocked selected companies.";

            }

            // Redirect back to the ViewEmailRecords action with the selected RecordType and SelectedDate
            return RedirectToAction("ChangeRecordType"); // Adjust as needed
        }

        [HttpPost]
        public async Task<IActionResult> ViewEmailRecords(string RecordType, DateTime? SelectedDate, string category, string? UserName)
        {
            var username = UserName;

            var model = new UploadResultViewModel
            {
                BlockCustomersEmailList = new List<ProspectCustomer>(),
                CleanCustomersEmailList = new List<ProspectCustomer>(),
                SelectedDate = SelectedDate,
                RecordType = RecordType,
                BlockedCustomers = new List<ProspectCustomer>(),
                CleanCustomers = new List<ProspectCustomer>()
            };

            // Parse the RecordType to determine if it's clean or blocked
            bool isClean = RecordType == "Clean";
            bool isBlocked = RecordType == "Blocked";

            // Blocked records: RecordType == 0 and IS_EMAIL_BLOCKED == true
            if (isBlocked)
            {
                model.BlockCustomersEmailList = await _context.Prospects
                    .Where(c => c.RECORD_TYPE == true && c.IS_EMAIL_BLOCKED == true && c.CREATED_BY == username && (string.IsNullOrEmpty(category) || c.CATEGORY == category) &&
                                (!SelectedDate.HasValue || c.CREATED_ON.Value.Date == SelectedDate.Value.Date))
                    .ToListAsync();
                if (model.BlockCustomersEmailList.Any())
                {
                    TempData["messagesuccess"] = "Record Found Succesfully";
                }
                else
                {
                    TempData["message"] = "No record Found";
                }
            }
            // Clean records: RecordType == 0 and IS_EMAIL_BLOCKED == false
            else if (isClean)
            {
                model.CleanCustomersEmailList = await _context.Prospects
                    .Where(c => c.RECORD_TYPE == false && c.IS_EMAIL_BLOCKED == false && c.CREATED_BY == username && (string.IsNullOrEmpty(category) || c.CATEGORY == category) &&
                                (!SelectedDate.HasValue || c.CREATED_ON.Value.Date == SelectedDate.Value.Date))
                    .ToListAsync();
                if (model.CleanCustomersEmailList.Any())
                {
                    TempData["messagesuccess"] = "Record Found Successfully";
                }
                else
                {
                    TempData["message"] = "No Record Found";
                }

            }
            // If no specific record type is selected, show both Blocked and Clean records for the given date
            else
            {
                model.BlockCustomersEmailList = await _context.Prospects
                    .Where(c => c.RECORD_TYPE == true && c.IS_EMAIL_BLOCKED == true && c.CREATED_BY == username && (string.IsNullOrEmpty(category) || c.CATEGORY == category) &&
                                (!SelectedDate.HasValue || c.CREATED_ON.Value.Date == SelectedDate.Value.Date))
                    .ToListAsync();

                model.CleanCustomersEmailList = await _context.Prospects
                    .Where(c => c.RECORD_TYPE == false && c.IS_EMAIL_BLOCKED == false && c.CREATED_BY == username && (string.IsNullOrEmpty(category) || c.CATEGORY == category) &&
                                (!SelectedDate.HasValue || c.CREATED_ON.Value.Date == SelectedDate.Value.Date))
                    .ToListAsync();

                if (model.BlockCustomersEmailList.Any() || model.CleanCustomersEmailList.Any())
                {
                    TempData["messagesuccess"] = "Records found Successfully";
                }
                else
                {
                    TempData["message"] = "No Record found";
                }

            }
            var users = await _context.Users.ToListAsync();

            // Ensure ViewBag.Users is populated after form submission
            ViewBag.Users = new SelectList(users, "Username", "Username");
            return View("ChangeRecordType", model); // Return the view with the populated UploadResultViewModel
        }


        [HttpPost]
        public async Task<IActionResult> FilterRecords(string Category, string UserName )
        {
            var users = await _context.Users.ToListAsync();
            ViewBag.Users = new SelectList(users, "Username", "Username");

            var recordsQuery = _context.Prospects.AsQueryable();

            if (!string.IsNullOrEmpty(Category) && !string.IsNullOrEmpty(UserName))
            {
                recordsQuery = recordsQuery.Where(r => r.CATEGORY == Category && !r.RECORD_TYPE && r.CREATED_BY==UserName);
            }
            if (!string.IsNullOrEmpty(UserName))
            {
                recordsQuery = recordsQuery.Where(r=>!r.RECORD_TYPE && r.CREATED_BY == UserName);
            }

            var model = new AssignToViewModel
            {
                RecordsList = await recordsQuery.ToListAsync()
            };

            TempData["message"] = model.RecordsList.Any() ? "Records Found" : "No Records Found";
            return View("AssignRecords", model);
        }


        [HttpPost]
        public async Task<IActionResult> Assign(int[] RecordIds, string UserName, bool assignAll)
        {
            if (string.IsNullOrEmpty(UserName))
            {
                TempData["message"] = "Please select a user to assign records.";
                return RedirectToAction("AssignRecords");
            }

            IQueryable<ProspectCustomer> recordsQuery = _context.Prospects.AsQueryable();

            if (assignAll)
            {
                recordsQuery = recordsQuery.Where(r => !r.IS_EMAIL_BLOCKED);
            }
            else if (RecordIds != null && RecordIds.Length > 0)
            {
                recordsQuery = recordsQuery.Where(r => RecordIds.Contains(r.ID));
            }
            else
            {
                TempData["message"] = "No records selected for assignment.";
                return RedirectToAction("AssignTo");
            }

            var recordsToAssign = await recordsQuery.ToListAsync();

            foreach (var record in recordsToAssign)
            {
                //record.ASSIGNED_TO = UserName; // Assuming there is an `ASSIGNED_TO` field in the model
                //record.UPDATED_ON = DateTime.Now;
            }

            await _context.SaveChangesAsync();

            TempData["messagesuccess"] = $"{recordsToAssign.Count} records successfully assigned to {UserName}.";
            return RedirectToAction("AssignRecords");
        }

    }
}
