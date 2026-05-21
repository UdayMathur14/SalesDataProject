using ClosedXML.Excel;
using DocumentFormat.OpenXml.Bibliography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SalesDataProject.Models;
using System;
using System.Text.RegularExpressions;

namespace SalesDataProject.Controllers
{
    public class CustomerController : Controller
    {
        private readonly AppDbContext _context;
        public CustomerController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var canAccessCustomer = HttpContext.Session.GetString("CanAccessCustomer");
                if (canAccessCustomer != "True")
                {
                    // If not authorized, redirect to home or another page
                    return RedirectToAction("Login", "Auth");
                }

                var countries = await _context.Countries.ToListAsync();
                var phoneCodes = await _context.Countries.Select(c => c.CountryCode).Distinct().ToListAsync();

                // Pass countries and phone codes separately to the view
                ViewBag.Countries = new SelectList(countries, "CountryName", "CountryName");
                ViewBag.CountryCodes = new SelectList(phoneCodes);

                // Fetch recent customers to display on the page (most recent first)
                var customers = await _context.Customers
                    .AsNoTracking()
                    .OrderByDescending(c => c.CREATED_ON)
                    .Take(1000)
                    .ToListAsync();

                ViewBag.Customers = customers;

                return View();
            }
            catch (Exception ex)
            {
                return RedirectToAction("Login", "Auth");
            }
        }
        public async Task<IActionResult> ViewCustomers(Customer model)
        {
            try
            {
                var Customers = await _context.Customers.ToListAsync();
                return View(Customers);
            }
            catch (Exception ex)
            {
                return RedirectToAction("Login", "Auth");
            }
        }
        public IActionResult ShowInvalidRecords()
        {
            try
            {
                var recordsJson = HttpContext.Session.GetString("InvalidRecords"); // Changed from TempData
                if (!string.IsNullOrEmpty(recordsJson))
                {
                    var invalidRecords = JsonConvert.DeserializeObject<List<InvalidCustomerRecord>>(recordsJson);
                    return View("InvalidRecords", invalidRecords);
                }
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                return RedirectToAction("Login", "Auth");
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create(Customer customer)
        {
            var username = HttpContext.Session.GetString("Username");
            customer.CREATED_BY = username;
            customer.MODIFIED_BY = username;
            customer.CUSTOMER_EMAIL.ToLower();
            customer.EMAIL_DOMAIN = customer.CUSTOMER_EMAIL.Split('@').Last();

            try
            {
                // Attempt to add the new customer to the context
                _context.Customers.Add(customer);
                var existingCustomer = _context.Customers.FirstOrDefault(c => c.CUSTOMER_EMAIL.ToLower() == customer.CUSTOMER_EMAIL.Trim().ToLower() || c.COMPANY_NAME.ToUpper() == c.COMPANY_NAME.ToUpper());
                var existingSalesCustomer = _context.CleanProspects.FirstOrDefault(c => c.CUSTOMER_EMAIL.ToLower() == customer.CUSTOMER_EMAIL.Trim().ToLower() || c.COMPANY_NAME.ToUpper() == customer.COMPANY_NAME.ToUpper() || (c.EMAIL_DOMAIN.ToLower() == c.EMAIL_DOMAIN.ToLower()));
                if (existingCustomer != null || existingSalesCustomer != null)
                {
                    ModelState.AddModelError("CUSTOMER_EMAIL", "This customer Email already exists.");
                    TempData["Message"] = "This customer Email already exists.";
                    TempData["MessageType"] = "Error";
                    return RedirectToAction(nameof(Index));
                }

                await _context.SaveChangesAsync();
                TempData["Message"] = "Customer has been successfully created.";
                TempData["MessageType"] = "Success";
                return RedirectToAction(nameof(ViewCustomers));
            }
            catch (DbUpdateException ex)
            {
                // Check if the error is related to the unique constraint violation
                if (ex.InnerException is SqlException sqlEx && sqlEx.Number == 2627) // 2627 is the SQL error code for unique constraint violation
                {
                    ModelState.AddModelError("CUSTOMER_CODE", "This customer code already exists.");
                    TempData["Message"] = "This customer code already exists.";
                    TempData["MessageType"] = "Error";
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    // Handle other types of exceptions as necessary
                    ModelState.AddModelError(string.Empty, "An error occurred while saving the customer.");
                    TempData["Message"] = "An error occurred while saving the customer.";
                    TempData["MessageType"] = "Error";
                    return RedirectToAction(nameof(Index));
                }
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadExcel(IFormFile file)
        {
            var username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrWhiteSpace(username))
            {
                TempData["Message"] = "Session expired. Please login again.";
                TempData["MessageType"] = "Error";
                return RedirectToAction("Login", "Auth");
            }

            if (file == null || file.Length == 0)
            {
                TempData["Message"] = "File is empty. Please upload a valid Excel file.";
                TempData["MessageType"] = "Error";
                return RedirectToAction(nameof(ViewCustomers));
            }

            var invalidRecords = new List<InvalidCustomerRecord>();
            var duplicateRecords = new List<InvalidCustomerRecord>();
            var newCustomers = new List<Customer>();

            try
            {
                using (var stream = new MemoryStream())
                {
                    await file.CopyToAsync(stream);
                    stream.Position = 0;

                    using (var workbook = new XLWorkbook(stream))
                    {
                        var worksheet = workbook.Worksheet(1);
                        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;

                        if (lastRow < 3)
                        {
                            TempData["Message"] = "Excel file has no data to process.";
                            TempData["MessageType"] = "Error";
                            return RedirectToAction(nameof(ViewCustomers));
                        }

                        // STEP 1: Pre-fetch common domains to avoid DB calls in loop
                        var commonDomains = await _context.CommonDomains
                            .AsNoTracking()
                            .Select(d => d.DomainName.ToLower())
                            .ToListAsync();
                        var commonDomainSet = new HashSet<string>(commonDomains);

                        // STEP 2: Extract all emails & company names from Excel to check duplicates in one go
                        var emailsInExcel = new HashSet<string>();
                        var companiesInExcel = new HashSet<string>();

                        for (int row = 3; row <= lastRow; row++)
                        {
                            emailsInExcel.Add(worksheet.Cell(row, 5).GetString().Trim().ToLowerInvariant());
                            companiesInExcel.Add(worksheet.Cell(row, 2).GetString().Trim().ToUpper());
                        }

                        // STEP 3: Fetch matching records from DB (Only ONE DB Call per table)
                        var existingCustomers = await _context.Customers.AsNoTracking()
                            .Where(c => emailsInExcel.Contains(c.CUSTOMER_EMAIL.ToLower()) || companiesInExcel.Contains(c.COMPANY_NAME.ToUpper()))
                            .Select(c => new { Email = c.CUSTOMER_EMAIL.ToLower(), Company = c.COMPANY_NAME.ToUpper() })
                            .ToListAsync();

                        var existingProspects = await _context.CleanProspects.AsNoTracking()
                            .Where(p => emailsInExcel.Contains(p.CUSTOMER_EMAIL.ToLower()) || companiesInExcel.Contains(p.COMPANY_NAME.ToUpper()))
                            .Select(p => new { Email = p.CUSTOMER_EMAIL.ToLower(), Company = p.COMPANY_NAME.ToUpper() })
                            .ToListAsync();

                        var existingCustomerEmails = new HashSet<string>(existingCustomers.Select(c => c.Email));
                        var existingCustomerCompanies = new HashSet<string>(existingCustomers.Select(c => c.Company));

                        var existingProspectEmails = new HashSet<string>(existingProspects.Select(p => p.Email));
                        var existingProspectCompanies = new HashSet<string>(existingProspects.Select(p => p.Company));

                        // STEP 4: Process Excel Rows
                        for (int row = 3; row <= lastRow; row++)
                        {
                            string companyName = worksheet.Cell(row, 2).GetString().Trim().ToUpper();
                            string contactPerson = worksheet.Cell(row, 3).GetString().Trim();
                            string customerNumber1 = worksheet.Cell(row, 4).GetString().Trim();
                            string customerEmail = worksheet.Cell(row, 5).GetString().Trim().ToLowerInvariant();
                            string countryCode = worksheet.Cell(row, 6).GetString().Trim();
                            string country = worksheet.Cell(row, 7).GetString().Trim();
                            string customerNumber2 = worksheet.Cell(row, 8).GetString().Trim();
                            string customerNumber3 = worksheet.Cell(row, 9).GetString().Trim();
                            string state = worksheet.Cell(row, 10).GetString().Trim().ToUpperInvariant();
                            string city = worksheet.Cell(row, 11).GetString().Trim().ToUpperInvariant();
                            string category = worksheet.Cell(row, 12).GetString().Trim().ToUpper();
                            string emailDomain = customerEmail?.Split('@').Last().ToLower();

                            // Skip common domains
                            if (commonDomainSet.Contains(emailDomain))
                            {
                                emailDomain = "-";
                            }

                            // Validate email
                            if (!IsValidEmail(customerEmail))
                            {
                                invalidRecords.Add(new InvalidCustomerRecord { RowNumber = row - 1, CompanyName = companyName, CustomerEmail = customerEmail, CustomerNumber = customerNumber1, ErrorMessage = "Invalid email format" });
                                continue;
                            }

                            // Validate phone numbers
                            if ((!IsValidPhoneNumber(customerNumber1) || !IsValidPhoneNumber(customerNumber2) || !IsValidPhoneNumber(customerNumber3)) && !string.IsNullOrEmpty(customerNumber1))
                            {
                                invalidRecords.Add(new InvalidCustomerRecord { RowNumber = row - 1, CompanyName = companyName, CustomerEmail = customerEmail, CustomerNumber = customerNumber1, ErrorMessage = "Invalid phone number" });
                                continue;
                            }

                            // Mandatory fields check
                            if (string.IsNullOrWhiteSpace(companyName) || string.IsNullOrWhiteSpace(customerEmail) || string.IsNullOrWhiteSpace(countryCode))
                            {
                                invalidRecords.Add(new InvalidCustomerRecord { RowNumber = row - 1, CompanyName = companyName, CustomerEmail = customerEmail, CustomerNumber = customerNumber1, ErrorMessage = "Missing mandatory fields" });
                                continue;
                            }

                            // Fast In-Memory Duplicate Check
                            bool existsInCustomer = existingCustomerEmails.Contains(customerEmail) || existingCustomerCompanies.Contains(companyName);
                            bool existsInProspect = existingProspectEmails.Contains(customerEmail) || existingProspectCompanies.Contains(companyName);

                            if (existsInCustomer || existsInProspect)
                            {
                                duplicateRecords.Add(new InvalidCustomerRecord { RowNumber = row - 1, CompanyName = companyName, CustomerEmail = customerEmail, CustomerNumber = customerNumber1, ErrorMessage = "Duplicate record found" });
                                continue;
                            }

                            // Add valid new customer
                            newCustomers.Add(new Customer
                            {
                                CUSTOMER_CODE = "1",
                                COMPANY_NAME = companyName,
                                CUSTOMER_EMAIL = customerEmail,
                                CONTACT_PERSON = contactPerson,
                                CUSTOMER_CONTACT_NUMBER1 = customerNumber1,
                                CUSTOMER_CONTACT_NUMBER2 = customerNumber2,
                                CUSTOMER_CONTACT_NUMBER3 = customerNumber3,
                                COUNTRY_CODE = countryCode,
                                COUNTRY = country,
                                STATE = state,
                                CITY = city,
                                CATEGORY = category,
                                EMAIL_DOMAIN = emailDomain,
                                CREATED_BY = username,
                                CREATED_ON = DateTime.UtcNow,
                                MODIFIED_BY = username,
                                MODIFIED_ON = DateTime.UtcNow
                            });
                        }

                        // Save valid customers in bulk
                        if (newCustomers.Any())
                        {
                            _context.Customers.AddRange(newCustomers);
                            await _context.SaveChangesAsync();
                        }
                    }
                }

                var allInvalid = invalidRecords.Concat(duplicateRecords).ToList();
                if (allInvalid.Any())
                {
                    // USE SESSION INSTEAD OF TEMPDATA FOR LARGE JSON STRINGS
                    HttpContext.Session.SetString("InvalidRecords", JsonConvert.SerializeObject(
                        allInvalid.Select(r => new { r.RowNumber, r.CompanyName, r.CustomerEmail, r.CustomerNumber, r.ErrorMessage })
                    ));

                    TempData["Message"] = "Some records were invalid or duplicates; valid records saved.";
                    TempData["MessageType"] = "Error";
                    return RedirectToAction(nameof(ShowInvalidRecords));
                }

                TempData["Message"] = "Excel uploaded successfully.";
                TempData["MessageType"] = "Success";
                return RedirectToAction(nameof(ViewCustomers));
            }
            catch (Exception ex)
            {
                TempData["Message"] = $"Unexpected error: {ex.Message}";
                TempData["MessageType"] = "Error";
                return RedirectToAction(nameof(ViewCustomers));
            }
        }
        // Helper method to validate email format
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


        [HttpGet]
        public IActionResult DownloadTemplate()
        {
            try
            {
                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("CustomerTemplate");

                    // Define the headers in the template
                    worksheet.Cell(1, 2).Value = "*CompanyName";
                    worksheet.Cell(1, 3).Value = "*ContactPerson";
                    worksheet.Cell(1, 4).Value = "ContactNo1";
                    worksheet.Cell(1, 5).Value = "*Email";
                    worksheet.Cell(1, 6).Value = "*CountryCode";
                    worksheet.Cell(1, 7).Value = "*Country";
                    worksheet.Cell(1, 8).Value = "ContactNo2";
                    worksheet.Cell(1, 9).Value = "ContactNo3";
                    worksheet.Cell(1, 10).Value = "State";
                    worksheet.Cell(1, 11).Value = "City";
                    worksheet.Cell(1, 12).Value = "*Category";

                    // Example data
                    worksheet.Cell(2, 2).Value = "Ennoble Ip";
                    worksheet.Cell(2, 3).Value = "Rajnish Sir";
                    worksheet.Cell(2, 4).Value = "123456789";
                    worksheet.Cell(2, 5).Value = "ennobleip@gmail.com";
                    worksheet.Cell(2, 6).Value = "+91";
                    worksheet.Cell(2, 7).Value = "INDIA";
                    worksheet.Cell(2, 8).Value = "9876543210";
                    worksheet.Cell(2, 9).Value = "9876543210";
                    worksheet.Cell(2, 10).Value = "DELHI";
                    worksheet.Cell(2, 11).Value = "NEW DELHI";
                    worksheet.Cell(2, 12).Value = "Corporate/Law Firm/MSME/University/PCT/Individual";

                    // Adjust column widths to fit content
                    worksheet.Columns().AdjustToContents();

                    // Optionally, apply styles to the header row for better visibility
                    var headerRow = worksheet.Range("A1:L1");
                    headerRow.Style.Font.Bold = true;
                    headerRow.Style.Font.FontColor = XLColor.Red;
                    headerRow.Style.Fill.BackgroundColor = XLColor.Yellow;
                    headerRow.Style.Border.TopBorder = XLBorderStyleValues.Thin;
                    headerRow.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                    headerRow.Style.Border.LeftBorder = XLBorderStyleValues.Thin;
                    headerRow.Style.Border.RightBorder = XLBorderStyleValues.Thin;

                    var row = worksheet.Range("A2:L2");
                    row.Style.Font.FontColor = XLColor.Black;

                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        var content = stream.ToArray();
                        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "CustomerTemplate.xlsx");
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["Message"] = "An unexpected error occurred. Please try again.";
                TempData["MessageType"] = "Error";
                return RedirectToAction(nameof(ViewCustomers));
            }

        }

        [HttpGet]
        public IActionResult ExportInvalidRecords()
        {
            try
            {
                // 1. TempData ki jagah ab Session se data retrieve karenge
                var invalidRecordsJson = HttpContext.Session.GetString("InvalidRecords");

                // If Session data is null or empty, create a placeholder record
                List<InvalidCustomerRecord> invalidRecords;
                if (string.IsNullOrEmpty(invalidRecordsJson))
                {
                    invalidRecords = new List<InvalidCustomerRecord>
            {
                new InvalidCustomerRecord
                {
                    RowNumber = 0,
                    CompanyName = "No records available",
                    CustomerEmail = "-",
                    CustomerNumber = "-",
                    ErrorMessage = "No invalid records found."
                }
            };
                }
                else
                {
                    invalidRecords = JsonConvert.DeserializeObject<List<InvalidCustomerRecord>>(invalidRecordsJson);
                }

                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("InvalidRecords");

                    // Adding headers
                    worksheet.Cell(1, 1).Value = "Excel Row";
                    worksheet.Cell(1, 2).Value = "Company Name";
                    worksheet.Cell(1, 3).Value = "Customer Email";
                    worksheet.Cell(1, 4).Value = "Customer Number";
                    worksheet.Cell(1, 5).Value = "Error Message";

                    // Populating data
                    for (int i = 0; i < invalidRecords.Count; i++)
                    {
                        var record = invalidRecords[i];
                        worksheet.Cell(i + 2, 1).Value = record.RowNumber;
                        worksheet.Cell(i + 2, 2).Value = record.CompanyName;
                        worksheet.Cell(i + 2, 3).Value = record.CustomerEmail;
                        worksheet.Cell(i + 2, 4).Value = record.CustomerNumber;
                        worksheet.Cell(i + 2, 5).Value = record.ErrorMessage;
                    }

                    worksheet.Columns().AdjustToContents();

                    // Apply styles to header row
                    var headerRow = worksheet.Range("A1:E1");
                    headerRow.Style.Font.Bold = true;
                    headerRow.Style.Font.FontColor = XLColor.White;
                    headerRow.Style.Fill.BackgroundColor = XLColor.BlueGray;

                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        stream.Position = 0;
                        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "InvalidRecords.xlsx");
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["Message"] = "An unexpected error occurred while exporting. Please try again.";
                TempData["MessageType"] = "Error";
                return RedirectToAction(nameof(ViewCustomers));
            }
        }
        public async Task<IActionResult> Countryget()
        {
            var countries = await _context.Countries
                .Select(c => new
                {
                    CountryId = c.CountryId.ToString(),
                    CountryName = c.CountryName,
                    CountryCode = c.CountryCode
                })
                .ToListAsync();

            // Check if the countries list is null or empty
            if (countries == null || !countries.Any())
            {
                
            }
            ViewData["CountryList"] = countries;  // Set the countries to ViewData
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> RecentUploadStatus(int minutes =60)
        {
            var username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrWhiteSpace(username))
            {
                return Json(new { success = false, message = "Session expired" });
            }

            var cutoff = DateTime.UtcNow.AddMinutes(-minutes);
            var recent = await _context.Customers
                .Where(c => c.CREATED_BY == username && c.CREATED_ON >= cutoff)
                .OrderByDescending(c => c.CREATED_ON)
                .ToListAsync();

            return Json(new
            {
                success = true,
                count = recent.Count,
                sample = recent.Take(20).Select(c => new { c.ID, c.COMPANY_NAME, c.CUSTOMER_EMAIL, CreatedOn = c.CREATED_ON.HasValue ? c.CREATED_ON.Value.ToString("yyyy-MM-dd HH:mm:ss") : "" })
            });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteRecentUploads(int minutes =60)
        {
            var username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrWhiteSpace(username))
            {
                return Json(new { success = false, message = "Session expired" });
            }

            var cutoff = DateTime.UtcNow.AddMinutes(-minutes);
            var recent = await _context.Customers
                .Where(c => c.CREATED_BY == username && c.CREATED_ON >= cutoff)
                .ToListAsync();

            if (!recent.Any())
            {
                return Json(new { success = true, deleted =0, message = "No recent uploads found to delete." });
            }

            try
            {
                _context.Customers.RemoveRange(recent);
                await _context.SaveChangesAsync();
                return Json(new { success = true, deleted = recent.Count });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult GetInvalidUploadRecords()
        {
            try
            {
                var invalidJson = HttpContext.Session.GetString("InvalidRecords"); // Changed from TempData
                if (string.IsNullOrEmpty(invalidJson)) return Json(new { success = false, message = "No invalid records available." });
                var invalid = JsonConvert.DeserializeObject<List<InvalidCustomerRecord>>(invalidJson);
                return Json(new { success = true, count = invalid.Count, records = invalid });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        public async Task<IActionResult> AllCustomers(int page =1, int pageSize =50, string search = null)
        {
            var username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrWhiteSpace(username))
            {
                TempData["Message"] = "Session Expired";
                TempData["MessageType"] = "Error";
                return RedirectToAction("Login", "Auth");
            }
            try
            {
                var query = _context.Customers.AsNoTracking().AsQueryable();
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim().ToLower();
                    query = query.Where(c => c.COMPANY_NAME.ToLower().Contains(s) || c.CUSTOMER_EMAIL.ToLower().Contains(s) || c.CONTACT_PERSON.ToLower().Contains(s));
                }
                var total = await query.CountAsync();
                var customers = await query.OrderByDescending(c => c.CREATED_ON)
                    .Skip((page -1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var vm = new SalesDataProject.Models.CustomerListViewModel
                {
                    Customers = customers,
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalCount = total
                };

                return View("AllCustomers", vm);
            }
            catch (Exception ex)
            {
                TempData["Message"] = "An error occurred while loading customers.";
                TempData["MessageType"] = "Error";
                return RedirectToAction("Index");
            }
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> ViewCustomers(int pageNumber = 1, int pageSize = 10)
        {
            // Fallback taaki galat page number na pass ho sake
            if (pageNumber < 1) pageNumber = 1;

            // Total records count nikalna zaroori hai pagination controls ke liye
            var totalRecords = await _context.Customers.CountAsync();

            // Sirf wahi data fetch hoga jo current page par dikhana hai (.Skip aur .Take se)
            var customers = await _context.Customers
                .AsNoTracking()
                .OrderByDescending(c => c.CREATED_ON) // Ya jis column se sort karna chaho
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Saari pagination values ViewBag mein daal dete hain taaki HTML me use ho sake
            ViewBag.CurrentPage = pageNumber;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize);
            ViewBag.TotalRecords = totalRecords;

            return View(customers);
        }
    }
}
