using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SalesDataProject.Models;
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
            return View();
        }
        public async Task<IActionResult> ViewCustomers(Customer model)
        {
            var Customers = await _context.Customers.ToListAsync();
            return View(Customers);

        }
        public IActionResult ShowInvalidRecords()
        {
            if (TempData["InvalidRecords"] != null)
            {
                var recordsJson = TempData["InvalidRecords"].ToString();
                var invalidRecords = JsonConvert.DeserializeObject<List<Customer>>(recordsJson);
                return View("InvalidRecords", invalidRecords); // Specify the view name if it's not the default
            }

            return RedirectToAction("Index"); // Redirect to a fallback if no data is available
        }



        [HttpPost]
        public async Task<IActionResult> Create(Customer customer)
        {
            var username = HttpContext.Session.GetString("Username");
            customer.CREATED_BY = username;
            customer.MODIFIED_BY = username;
            customer.CUSTOMER_EMAIL.ToLower();
            customer.EmailDomain = customer.CUSTOMER_EMAIL.Split('@').Last();

            try
            {
                // Attempt to add the new customer to the context
                _context.Customers.Add(customer);
                var existingCustomer = _context.Customers.FirstOrDefault(c =>
     c.CUSTOMER_EMAIL.ToLower() == customer.CUSTOMER_EMAIL.Trim().ToLower() ||
     c.CUSTOMER_CONTACT_NUMBER1 == customer.CUSTOMER_CONTACT_NUMBER1 ||
     (customer.CUSTOMER_CONTACT_NUMBER2 != null && c.CUSTOMER_CONTACT_NUMBER2 == customer.CUSTOMER_CONTACT_NUMBER2) ||
     (customer.CUSTOMER_CONTACT_NUMBER3 != null && c.CUSTOMER_CONTACT_NUMBER3 == customer.CUSTOMER_CONTACT_NUMBER3) ||
     (!string.IsNullOrEmpty(customer.EmailDomain) && c.EmailDomain == customer.EmailDomain && c.COUNTRY == customer.COUNTRY)
 );
                //var existingCustomer = _context.Customers.FirstOrDefault(c => c.CUSTOMER_EMAIL.ToLower() == customer.CUSTOMER_EMAIL.Trim().ToLower() || c.CUSTOMER_CONTACT_NUMBER1 == customer.CUSTOMER_CONTACT_NUMBER1 || c.CUSTOMER_CONTACT_NUMBER2 == customer.CUSTOMER_CONTACT_NUMBER2 || c.CUSTOMER_CONTACT_NUMBER3 == customer.CUSTOMER_CONTACT_NUMBER3 || (!string.IsNullOrEmpty(customer.EmailDomain) && c.EmailDomain == customer.EmailDomain && c.COUNTRY==customer.COUNTRY));
                if (existingCustomer != null)
                {
                    ModelState.AddModelError("CUSTOMER_EMAIL", "This customer Emial already exists.");
                    TempData["ErrorMessage"] = "This customer Email already exists.";
                    return RedirectToAction(nameof(Index));
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Customer has been successfully created.";
                return RedirectToAction(nameof(ViewCustomers));
            }
            catch (DbUpdateException ex)
            {
                // Check if the error is related to the unique constraint violation
                if (ex.InnerException is SqlException sqlEx && sqlEx.Number == 2627) // 2627 is the SQL error code for unique constraint violation
                {
                    ModelState.AddModelError("CUSTOMER_CODE", "This customer code already exists.");
                    TempData["ErrorMessage"] = "This customer code already exists.";
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    // Handle other types of exceptions as necessary
                    ModelState.AddModelError(string.Empty, "An error occurred while saving the customer.");
                    TempData["ErrorMessage"] = "An error occurred while saving the customer.";
                    return RedirectToAction(nameof(Index));
                }
            }
        }




        [HttpPost]
        public async Task<IActionResult> UploadExcel(IFormFile file)
        {
            var username = HttpContext.Session.GetString("Username");
            if (file == null || file.Length == 0)
            {
                TempData["ErrorMessage"] = "File is empty. Please upload a valid Excel file.";
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
                    stream.Position = 0; // Reset stream position

                    using (var workbook = new XLWorkbook(stream))
                    {
                        var worksheet = workbook.Worksheet(1); // Use the first worksheet
                        var lastRow = worksheet.LastRowUsed().RowNumber();

                        var customersFromExcel = new List<Customer>();

                        for (int row = 3; row <= lastRow; row++) // Start reading data from row 3
                        {
                            var companyName = worksheet.Cell(row, 2).GetString();
                            var contactPerson = worksheet.Cell(row, 3).GetString()?.ToUpperInvariant();
                            var customerNumber = worksheet.Cell(row, 4).GetString();
                            var customerEmail = worksheet.Cell(row, 5).GetString()?.ToLowerInvariant();
                            var countryCode = worksheet.Cell(row, 6).GetString()?.Trim();
                            var country = worksheet.Cell(row, 7).GetString();
                            var category = worksheet.Cell(row, 12).GetString().ToUpper().Trim();

                            // Validation
                            if (!IsValidEmail(customerEmail))
                            {
                                invalidRecords.Add(new InvalidCustomerRecord
                                {
                                    RowNumber = row,
                                    CompanyName = companyName,
                                    CustomerEmail = customerEmail,
                                    CustomerNumber = customerNumber,
                                    ErrorMessage = "Invalid email format."
                                });
                                continue;
                            }

                            if (string.IsNullOrWhiteSpace(companyName) || string.IsNullOrWhiteSpace(customerNumber) ||
                                string.IsNullOrWhiteSpace(customerEmail) || string.IsNullOrWhiteSpace(countryCode)|| string.IsNullOrWhiteSpace(category))
                            {
                                invalidRecords.Add(new InvalidCustomerRecord
                                {
                                    RowNumber = row,
                                    CompanyName = companyName,
                                    CustomerEmail = customerEmail,
                                    CustomerNumber = customerNumber,
                                    ErrorMessage = "Missing mandatory fields!"
                                });
                                continue;
                            }

                            if (!new[] { "CORPORATE", "LAWFIRM", "SME", "UNIVERSITY" }.Contains(category?.ToUpperInvariant()))
                            {
                                invalidRecords.Add(new InvalidCustomerRecord
                                {
                                    RowNumber = row,
                                    CompanyName = companyName,
                                    CustomerEmail = customerEmail,
                                    CustomerNumber = customerNumber,
                                    ErrorMessage = "Invalid category."
                                });
                                continue;
                            }


                            // Add to the list of customers
                            customersFromExcel.Add(new Customer
                            {
                                CUSTOMER_CODE = worksheet.Cell(row, 1).GetString(),
                                COMPANY_NAME = companyName,
                                CUSTOMER_EMAIL = customerEmail,
                                CONTACT_PERSON = contactPerson,
                                CUSTOMER_CONTACT_NUMBER1 = customerNumber,
                                CountryCode = countryCode,
                                COUNTRY = country,
                                CITY = worksheet.Cell(row, 11).GetString()?.ToUpperInvariant(),
                                STATE = worksheet.Cell(row, 10).GetString()?.ToUpperInvariant(),
                                CUSTOMER_CONTACT_NUMBER2 = worksheet.Cell(row, 8).GetString(),
                                CUSTOMER_CONTACT_NUMBER3 = worksheet.Cell(row, 9).GetString(),
                                CREATED_BY = username,
                                CREATED_ON = DateTime.UtcNow,
                                MODIFIED_BY = username,
                                MODIFIED_ON = DateTime.UtcNow,
                                EmailDomain = customerEmail?.Split('@').Last(),
                                Category = category
                            });
                        }

                        // Retrieve the full customer records from the database, including CREATED_BY
                        var dbCustomers = _context.Customers
                            .Select(c => new
                            {
                                c.CUSTOMER_EMAIL,
                                c.CountryCode,
                                c.Category,
                                c.CREATED_BY // Include the CREATED_BY field in the selection
                            })
                            .ToList();

                        // Identify duplicate records based on matching email, country code, and category
                        duplicateRecords = customersFromExcel
     .Where(c =>
     {
         // Validate based on category type
         if (c.Category == "CORPORATE" || c.Category == "SME")
         {
             return dbCustomers
                 .Any(db =>
                     db.CUSTOMER_EMAIL.ToLowerInvariant().Trim() == c.CUSTOMER_EMAIL.ToLowerInvariant().Trim() &&
                     db.CountryCode.Trim() == c.CountryCode.Trim() &&
                     db.Category.ToUpperInvariant() == c.Category.ToUpperInvariant());
         }
         else if (c.Category == "UNIVERSITY" || c.Category == "LAWFIRM")
         {
             return dbCustomers
                 .Any(db =>
                     db.CUSTOMER_EMAIL.ToLowerInvariant().Trim() == c.CUSTOMER_EMAIL.ToLowerInvariant().Trim() &&
                     db.Category.ToUpperInvariant() == c.Category.ToUpperInvariant());
         }
         return false; // If none of the conditions match, no duplicates are found.
     })
     .Select(c =>
     {
         // Find the full customer record that matches the criteria (including CREATED_BY)
         var existingCustomer = dbCustomers
             .FirstOrDefault(db =>
                 db.CUSTOMER_EMAIL.ToLowerInvariant().Trim() == c.CUSTOMER_EMAIL.ToLowerInvariant().Trim() &&
                 (c.Category == "CORPORATE" || c.Category == "SME"
                     ? db.CountryCode.Trim() == c.CountryCode.Trim() && db.Category.ToUpperInvariant() == c.Category.ToUpperInvariant()
                     : db.Category.ToUpperInvariant() == c.Category.ToUpperInvariant()));

         // Get the CreatedBy field from the existing customer record
         var createdBy = existingCustomer?.CREATED_BY ?? "Unknown"; // Default to "Unknown" if null

         // Return the InvalidCustomerRecord with the CreatedBy info
         return new InvalidCustomerRecord
         {
             RowNumber = customersFromExcel.IndexOf(c) + 3, // Excel row index adjustment
             CompanyName = c.COMPANY_NAME,
             CustomerEmail = c.CUSTOMER_EMAIL,
             CustomerNumber = c.CUSTOMER_CONTACT_NUMBER1,
             ErrorMessage = $"Customer already exists with matching email, category, and country code (if applicable). Created by: {createdBy}"
         };
     })
     .ToList();


                        // Identify new customers (those that do not match any existing record)
                        newCustomers = customersFromExcel
                            .Where(c => !duplicateRecords
                                .Any(d => d.CustomerEmail.ToLowerInvariant().Trim() == c.CUSTOMER_EMAIL.ToLowerInvariant().Trim()))
                            .ToList();


                        // Save valid new customers
                        if (newCustomers.Any())
                        {
                            _context.Customers.AddRange(newCustomers);
                            await _context.SaveChangesAsync();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error processing file: {ex.Message}";
                return RedirectToAction(nameof(ViewCustomers));
            }

            // Combine all invalid records and display
            var allInvalidRecords = invalidRecords.Concat(duplicateRecords).ToList();
            if (allInvalidRecords.Any())
            {
                TempData["ErrorMessage"] = "Some records were invalid or duplicates.";
                TempData["InvalidRecords"] = JsonConvert.SerializeObject(allInvalidRecords);
                return View("InvalidRecords", allInvalidRecords);
            }

            TempData["SuccessMessage"] = "File uploaded successfully.";
            return RedirectToAction(nameof(ViewCustomers));
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
            // Regular expression to match exactly 10 digits
            string pattern = @"^\d{10}$";
            Regex regex = new Regex(pattern);

            // Check if the customer number matches the regex pattern
            return regex.IsMatch(customerNumber);
        }

        [HttpGet]
        public IActionResult DownloadTemplate()
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("CustomerTemplate");

                // Define the headers in the template
                worksheet.Cell(1, 1).Value = "CUSTOMER_CODE";
                worksheet.Cell(1, 2).Value = "COMPANY_NAME*";
                worksheet.Cell(1, 3).Value = "CONTACT_PERSON*";
                worksheet.Cell(1, 4).Value = "CONTACT_NO1*";
                worksheet.Cell(1, 5).Value = "EMAIL*";
                worksheet.Cell(1, 6).Value = "COUNTRY CODE*";
                worksheet.Cell(1, 7).Value = "COUNTRY*";
                worksheet.Cell(1, 8).Value = "CONTACT_NO2";
                worksheet.Cell(1, 9).Value = "CONTACT_NO3";
                worksheet.Cell(1, 10).Value = "STATE";
                worksheet.Cell(1, 11).Value = "CITY";
                worksheet.Cell(1, 12).Value = "CATEGORY*";

                // Example data
                worksheet.Cell(2, 1).Value = "Example";
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
                worksheet.Cell(2, 12).Value = "CORPORATE/LAWFIRM/SME/UNIVERSITY";

                // Adjust column widths to fit content
                worksheet.Columns().AdjustToContents();

                // Optionally, apply styles to the header row for better visibility
                var headerRow = worksheet.Range("A1:L1");
                headerRow.Style.Font.Bold = true;
                headerRow.Style.Font.FontColor = XLColor.Black;
                headerRow.Style.Fill.BackgroundColor = XLColor.BlueGray;

                var row = worksheet.Range("A2:L2");
                row.Style.Font.FontColor = XLColor.Red;

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "CustomerTemplate.xlsx");
                }
            }
        }


        [HttpGet]
        public IActionResult ExportInvalidRecords()
        {
            // Retrieve the invalid records from TempData
            var invalidRecordsJson = TempData["InvalidRecords"] as string;
            if (string.IsNullOrEmpty(invalidRecordsJson))
            {
                TempData["ErrorMessage"] = "No data available for export.";
                return RedirectToAction(nameof(Index));
            }

            var invalidRecords = JsonConvert.DeserializeObject<List<InvalidCustomerRecord>>(invalidRecordsJson);

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("InvalidRecords");

                // Adding headers
                worksheet.Cell(1, 1).Value = "Excel Row";
                worksheet.Cell(1, 2).Value = "Customer Name";
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

                // Optionally, apply styles to the header row for better visibility
                var headerRow = worksheet.Range("A1:L1");
                headerRow.Style.Font.Bold = true;
                headerRow.Style.Font.FontColor = XLColor.White;
                headerRow.Style.Fill.BackgroundColor = XLColor.BlueGray;

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    stream.Position = 0;

                    // Return the Excel file as a downloadable file
                    return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "InvalidRecords.xlsx");
                }
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
                // Handle the case when no countries are returned, maybe log an error
                // or set a default message.
            }

            ViewData["CountryList"] = countries;  // Set the countries to ViewData
            return View();
        }



    }
}
