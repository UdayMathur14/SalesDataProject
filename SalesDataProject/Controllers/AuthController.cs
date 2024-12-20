// Controllers/AuthController.cs
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Presentation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SalesDataProject.Models;
using SalesDataProject.Models.AuthenticationModels;
using System.Text.RegularExpressions;

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
        public IActionResult AddRecord()
        {
            var domains = _context.CommonDomains.ToList();
            return View(domains);
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
        public async Task<IActionResult> FilterRecords(string Category, string UserName)
        {
            var users = await _context.Users.ToListAsync();
            ViewBag.Users = new SelectList(users, "Username", "Username");

            var recordsQuery = _context.Prospects.AsQueryable();

            if (!string.IsNullOrEmpty(Category) && !string.IsNullOrEmpty(UserName))
            {
                recordsQuery = recordsQuery.Where(r => r.CATEGORY == Category && !r.RECORD_TYPE && r.CREATED_BY == UserName);
            }
            if (!string.IsNullOrEmpty(UserName))
            {
                recordsQuery = recordsQuery.Where(r => !r.RECORD_TYPE && r.CREATED_BY == UserName);
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

        [HttpPost]
        public IActionResult AddDomain(string domainName)
        {
            if (string.IsNullOrEmpty(domainName))
            {
                TempData["Error"] = "Domain name cannot be empty.";
                return RedirectToAction(nameof(AddRecord));
            }

            if (_context.CommonDomains.Any(d => d.DomainName == domainName))
            {
                TempData["Error"] = "This domain already exists.";
                return RedirectToAction(nameof(AddRecord));
            }

            var domain = new CommonDomains { DomainName = domainName };
            _context.CommonDomains.Add(domain);
            _context.SaveChanges();

            TempData["Success"] = "Domain added successfully!";
            return RedirectToAction(nameof(AddRecord));
        }


        public async Task<IActionResult> UploadRecord(IFormFile file)
        {
            var username = HttpContext.Session.GetString("Username");
            if (file == null || file.Length == 0)
            {
                TempData["ErrorMessage"] = "File is empty. Please upload a valid Excel file.";
                return RedirectToAction(nameof(AddRecord));
            }

            var newCustomers = new List<ProspectCustomer>();
            var invalidRecords = new List<InvalidCustomerRecord>();
            using (var stream = new MemoryStream())
            {
                await file.CopyToAsync(stream);
                stream.Position = 0; // Reset stream position

                using (var workbook = new XLWorkbook(stream))
                {
                    var worksheet = workbook.Worksheet(1); // Use the first worksheet
                    var lastRow = worksheet.LastRowUsed().RowNumber();

                    var customersFromExcel = new List<ProspectCustomer>();

                    for (int row = 3; row <= lastRow; row++) // Start reading data from row 3
                    {
                        var companyName = worksheet.Cell(row, 2).GetString().ToUpper();
                        var contactPerson = worksheet.Cell(row, 3).GetString();
                        var customerNumber = worksheet.Cell(row, 4).GetString();
                        var customerEmail = worksheet.Cell(row, 5).GetString()?.ToLowerInvariant();
                        var countryCode = worksheet.Cell(row, 6).GetString()?.Trim();
                        var country = worksheet.Cell(row, 7).GetString();
                        var customerNumber2 = worksheet.Cell(row, 8).GetString();
                        var customerNumber3 = worksheet.Cell(row, 9).GetString();
                        var category = worksheet.Cell(row, 12).GetString().ToUpper().Trim();
                        var recordtype = worksheet.Cell(row, 13).GetString().ToUpper().Trim();

                        // Validation
                        if (!IsValidEmail(customerEmail))
                        {
                            invalidRecords.Add(new InvalidCustomerRecord
                            {
                                RowNumber = row - 1,
                                CompanyName = companyName,
                                CustomerEmail = customerEmail,
                                CustomerNumber = customerNumber,
                                ErrorMessage = "Invalid email format."
                            });
                            continue;
                        }
                        if ((!IsValidPhoneNumber(customerNumber) || !IsValidPhoneNumber(customerNumber2) || !IsValidPhoneNumber(customerNumber3)) && (customerNumber != "" || customerNumber != null))
                        {
                            invalidRecords.Add(new InvalidCustomerRecord
                            {
                                RowNumber = row - 1,
                                CompanyName = companyName,
                                CustomerEmail = customerEmail,
                                CustomerNumber = customerNumber,
                                ErrorMessage = "Invalid Phone Number"
                            });
                            continue;
                        }


                        if (string.IsNullOrWhiteSpace(companyName) ||
                            string.IsNullOrWhiteSpace(customerEmail) || string.IsNullOrWhiteSpace(countryCode) || string.IsNullOrWhiteSpace(category))
                        {
                            invalidRecords.Add(new InvalidCustomerRecord
                            {
                                RowNumber = row - 1,
                                CompanyName = companyName,
                                CustomerEmail = customerEmail,
                                CustomerNumber = customerNumber,
                                ErrorMessage = "Missing mandatory fields!"
                            });
                            continue;
                        }

                        if (!new[] { "Corporate", "CORPORATE", "LAWFIRM", "Law Firm", "SME", "UNIVERSITY", "University", "PCT" }.Contains(category?.ToUpperInvariant()))
                        {
                            invalidRecords.Add(new InvalidCustomerRecord
                            {
                                RowNumber = row - 1,
                                CompanyName = companyName,
                                CustomerEmail = customerEmail,
                                CustomerNumber = customerNumber,
                                ErrorMessage = "Invalid category."
                            });
                            continue;
                        }

                        // Add to the list of customers
                        var customerData = new ProspectCustomer
                        {
                            CUSTOMER_CODE = worksheet.Cell(row, 1).GetString(),
                            COMPANY_NAME = companyName,
                            CUSTOMER_EMAIL = customerEmail,
                            CONTACT_PERSON = contactPerson,
                            CUSTOMER_CONTACT_NUMBER1 = customerNumber,
                            COUNTRY_CODE = countryCode,
                            COUNTRY = country,
                            CITY = worksheet.Cell(row, 11).GetString()?.ToUpperInvariant(),
                            STATE = worksheet.Cell(row, 10).GetString()?.ToUpperInvariant(),
                            CUSTOMER_CONTACT_NUMBER2 = customerNumber2,
                            CUSTOMER_CONTACT_NUMBER3 = customerNumber3,
                            CREATED_BY = username,
                            CREATED_ON = DateTime.UtcNow,
                            MODIFIED_BY = username,
                            MODIFIED_ON = DateTime.UtcNow,
                            EMAIL_DOMAIN = customerEmail,
                            CATEGORY = category
                        };

                        if (recordtype == "clean")
                        {
                            customerData.RECORD_TYPE = true; // Blocked
                            customerData.IS_EMAIL_BLOCKED = true;
                        }
                        else
                        {
                            customerData.RECORD_TYPE = true; // Blocked
                            customerData.IS_EMAIL_BLOCKED = true;
                        }
                        _context.Prospects.Add(customerData);
                    }
                    await _context.SaveChangesAsync();

                }
            }
            return View(AddRecord);
        }


        private bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                // Use Regex to validate the email pattern
                var emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
                return emailRegex.IsMatch(email);
            }
            catch
            {
                return false;
            }
        }
        public bool IsValidPhoneNumber(string customerNumber)
        {
            // Regular expression to match only digits or an empty string
            string pattern = @"^\d*$";
            Regex regex = new Regex(pattern);

            // Check if the customer number matches the regex pattern
            return regex.IsMatch(customerNumber);
        }

    }
}
