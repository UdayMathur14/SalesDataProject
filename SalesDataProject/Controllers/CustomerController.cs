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
                if (TempData["InvalidRecords"] != null)
                {
                    var recordsJson = TempData["InvalidRecords"].ToString();
                    var invalidRecords = JsonConvert.DeserializeObject<List<Customer>>(recordsJson);
                    return View("InvalidRecords", invalidRecords); // Specify the view name if it's not the default
                }

                return RedirectToAction("Index"); // Redirect to a fallback if no data is available
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
                var existingSalesCustomer = _context.Prospects.FirstOrDefault(c => c.CUSTOMER_EMAIL.ToLower() == customer.CUSTOMER_EMAIL.Trim().ToLower() || c.COMPANY_NAME.ToUpper() == customer.COMPANY_NAME.ToUpper() || (c.EMAIL_DOMAIN.ToLower() == c.EMAIL_DOMAIN.ToLower() && c.RECORD_TYPE == true));
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
        public async Task<IActionResult> UploadExcel(IFormFile file)
        {
            try
            {
                var username = HttpContext.Session.GetString("Username");
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
                        stream.Position = 0; // Reset stream position

                        using (var workbook = new XLWorkbook(stream))
                        {
                            var worksheet = workbook.Worksheet(1); // Use the first worksheet
                            var lastRow = worksheet.LastRowUsed().RowNumber();

                            var customersFromExcel = new List<Customer>();

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
                                var emailDomain = customerEmail?.Split('@').Last().ToLower();

                                var isCommonDomain = await _context.CommonDomains
                                    .AnyAsync(d => d.DomainName.ToLower() == emailDomain);

                                if (isCommonDomain)
                                {
                                    emailDomain = "-"; // Set to null if it is a common domain
                                }

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

                                if (!new[] { "Corporate", "CORPORATE", "LAWFIRM", "Law Firm", "SME", "UNIVERSITY", "University", "PCT", "LAW FIRM" }.Contains(category?.ToUpperInvariant()))
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
                                customersFromExcel.Add(new Customer
                                {
                                    CUSTOMER_CODE = "1",
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
                                    EMAIL_DOMAIN = emailDomain,
                                    CATEGORY = category
                                });
                            }

                            // Identify duplicates within the Excel sheet itself (based on email or company name)
                            var excelDuplicates = customersFromExcel
                                .GroupBy(c => new { c.CUSTOMER_EMAIL, c.COMPANY_NAME })
                                .Where(g => g.Count() > 1)
                                .SelectMany(g => g)
                                .ToList();

                            // Retrieve the full customer records from the database, including CREATED_BY
                            var dbCustomers = _context.Customers
                                .Select(c => new
                                {
                                    c.CUSTOMER_EMAIL,
                                    c.COMPANY_NAME,
                                    c.CREATED_BY // Include the CREATED_BY field in the selection
                                })
                                .ToList();

                            // Retrieve the full prospect records from the database
                            var dbProspects = _context.Prospects
                                .Select(p => new
                                {
                                    p.CUSTOMER_EMAIL,
                                    p.COMPANY_NAME,
                                    p.EMAIL_DOMAIN,
                                    p.RECORD_TYPE,
                                    p.CREATED_BY // Include the CREATED_BY field in the selection
                                })
                                .ToList();

                            // Identify duplicate records (from both Excel and database)
                            duplicateRecords = customersFromExcel
                                .Where(c =>
                                {
                                    // Check if the customer exists in the Customers table
                                    var existingCustomer = dbCustomers.Any(db =>
                                        db.CUSTOMER_EMAIL.ToLowerInvariant().Trim() == c.CUSTOMER_EMAIL.ToLowerInvariant().Trim() ||
                                        db.COMPANY_NAME.ToLowerInvariant().Trim() == c.COMPANY_NAME.ToLowerInvariant().Trim());

                                    // Check if the customer exists in the Prospects table
                                    var existingProspect = dbProspects.Any(p =>
                                        p.CUSTOMER_EMAIL.ToLowerInvariant().Trim() == c.CUSTOMER_EMAIL.ToLowerInvariant().Trim() ||
                                        p.COMPANY_NAME.ToLowerInvariant().Trim() == c.COMPANY_NAME.ToLowerInvariant().Trim() ||
                                        (p.EMAIL_DOMAIN == c.CUSTOMER_EMAIL?.Split('@').Last() && p.RECORD_TYPE == true)); // Check blocked emails in Prospects

                                    // Check if the entry is a duplicate within the Excel sheet
                                    var isExcelDuplicate = excelDuplicates.Any(e =>
                                        e.CUSTOMER_EMAIL.ToLowerInvariant().Trim() == c.CUSTOMER_EMAIL.ToLowerInvariant().Trim() ||
                                        e.COMPANY_NAME.ToLowerInvariant().Trim() == c.COMPANY_NAME.ToLowerInvariant().Trim());

                                    return existingCustomer || existingProspect || isExcelDuplicate;
                                })
                                .Select(c =>
                                {
                                    // Find which table (Customer or Prospect) the duplicate was found in
                                    var existingCustomer = dbCustomers.FirstOrDefault(db =>
                                        db.CUSTOMER_EMAIL.ToLowerInvariant().Trim() == c.CUSTOMER_EMAIL.ToLowerInvariant().Trim() ||
                                        db.COMPANY_NAME.ToLowerInvariant().Trim() == c.COMPANY_NAME.ToLowerInvariant().Trim());

                                    var existingProspect = dbProspects.FirstOrDefault(p =>
                                        p.CUSTOMER_EMAIL.ToLowerInvariant().Trim() == c.CUSTOMER_EMAIL.ToLowerInvariant().Trim() ||
                                        p.COMPANY_NAME.ToLowerInvariant().Trim() == c.COMPANY_NAME.ToLowerInvariant().Trim() ||
                                        (p.EMAIL_DOMAIN == c.CUSTOMER_EMAIL?.Split('@').Last() && p.RECORD_TYPE == true));

                                    // Determine the origin of the existing record (database or Excel duplicate)
                                    var createdBy = existingCustomer?.CREATED_BY ?? existingProspect?.CREATED_BY ?? "Unknown";

                                    return new InvalidCustomerRecord
                                    {
                                        RowNumber = customersFromExcel.IndexOf(c) + 3,
                                        CompanyName = c.COMPANY_NAME,
                                        CustomerEmail = c.CUSTOMER_EMAIL,
                                        CustomerNumber = c.CUSTOMER_CONTACT_NUMBER1,
                                        ErrorMessage = $"Company already exists. Created by: {createdBy}"
                                    };
                                })
                                .ToList();

                            // Identify new customers
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
                    TempData["Message"] = $"Error processing file: {ex.Message}";
                    TempData["MessageType"] = "Error";
                    return RedirectToAction(nameof(ViewCustomers));
                }

                // Combine all invalid records and display
                var allInvalidRecords = invalidRecords.Concat(duplicateRecords).ToList();
                if (allInvalidRecords.Any())
                {
                    TempData["Message"] = "Invalid or duplicate records found; Valid records saved.";
                    TempData["MessageType"] = "Error";
                    TempData["InvalidRecords"] = JsonConvert.SerializeObject(allInvalidRecords);
                    return View("InvalidRecords", allInvalidRecords);
                }

                TempData["Message"] = "File uploaded successfully.";
                TempData["MessageType"] = "Success";
                return RedirectToAction(nameof(ViewCustomers));
            }
            catch (Exception ex)
            {
                TempData["Message"] = "An unexpected error occurred. Please try again.";
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
                    worksheet.Cell(2, 12).Value = "Corporate/Law Firm/SME/University/PCT";

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
                // Retrieve the invalid records from TempData
                var invalidRecordsJson = TempData["InvalidRecords"] as string;
                if (string.IsNullOrEmpty(invalidRecordsJson))
                {
                    TempData["Message"] = "No data available for export.";
                    TempData["MessageType"] = "Error";
                    return RedirectToAction(nameof(Index));
                }

                var invalidRecords = JsonConvert.DeserializeObject<List<InvalidCustomerRecord>>(invalidRecordsJson);

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

                    // Optionally, apply styles to the header row for better visibility
                    var headerRow = worksheet.Range("A1:L1");
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
                TempData["Message"] = "An unexpected error occurred. Please try again.";
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
    }
}
