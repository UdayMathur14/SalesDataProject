using ClosedXML.Excel;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Mvc;
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
        public IActionResult Index()
        {
            var canAccessCustomer = HttpContext.Session.GetString("CanAccessCustomer");
            if (canAccessCustomer!="True")
            {
                // If not authorized, redirect to home or another page
                return RedirectToAction("Login", "Auth");
            }
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
            customer.CREATED_BY = "Admin";
            customer.MODIFIED_BY = "Admin";

            try
            {
                // Attempt to add the new customer to the context
                _context.Customers.Add(customer);
                var existingCustomer = _context.Customers.FirstOrDefault(c => c.CUSTOMER_EMAIL.ToLower() == customer.CUSTOMER_EMAIL.Trim().ToLower());
                if (existingCustomer != null) {
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
            var invalidRecords = new List<InvalidCustomerRecord>(); // List to store invalid records
            var existingDuplicateRecords = new List<InvalidCustomerRecord>(); // List to store records that are duplicates in the database

            if (file != null && file.Length > 0)
            {
                using (var stream = new MemoryStream())
                {
                    await file.CopyToAsync(stream);
                    stream.Position = 0; // Reset stream position to the beginning

                    using (var workbook = new XLWorkbook(stream))
                    {
                        var worksheet = workbook.Worksheet(1); // Read from the first worksheet
                        var lastRow = worksheet.LastRowUsed().RowNumber();

                        // List to store customers from the Excel file
                        var customersFromExcel = new List<Customer>();

                        for (int row = 2; row <= lastRow; row++) // Start from the second row (skip header)
                        {
                            var customerEmail = worksheet.Cell(row, 7).GetString();
                            var customerName = worksheet.Cell(row, 2).GetString();
                            var customerNumber = worksheet.Cell(row, 4).GetString();

                            // Validate email format
                            if (!IsValidEmail(customerEmail))
                            {
                                // Store invalid record
                                invalidRecords.Add(new InvalidCustomerRecord
                                {
                                    RowNumber = row,
                                    CustomerName = customerName,
                                    CustomerEmail = customerEmail,
                                    CustomerNumber = customerNumber,
                                    ErrorMessage = "Invalid email format."
                                });
                                continue; // Skip to the next row
                            }
                            else if (worksheet.Cell(row, 2).GetString() == "" || worksheet.Cell(row, 4).GetString() == "")
                            {
                                invalidRecords.Add(new InvalidCustomerRecord
                                {
                                    RowNumber = row,
                                    CustomerName = worksheet.Cell(row, 2).GetString(),
                                    CustomerEmail = customerEmail,
                                    CustomerNumber = worksheet.Cell(row, 4).GetString(),
                                    ErrorMessage = "Empty CustomerName or CustomerNumber"
                                });
                                continue;
                            }

                            // Check for duplicates in the list of customers
                            if (customersFromExcel.Any(c => c.CUSTOMER_EMAIL.ToLower().Trim() == customerEmail.ToLower().Trim()))
                            {
                                // Store duplicate record
                                invalidRecords.Add(new InvalidCustomerRecord
                                {
                                    RowNumber = row,
                                    CustomerName = customerName,
                                    CustomerEmail = customerEmail,
                                    CustomerNumber = customerNumber,
                                    ErrorMessage = "Duplicate email in the uploaded file."
                                });
                                continue; // Skip to the next row
                            }

                            // Create a customer object
                            var customer = new Customer
                            {
                                CUSTOMER_CODE = worksheet.Cell(row, 1).GetString(),
                                CUSTOMER_NAME = customerName,
                                CUSTOMER_EMAIL = customerEmail,
                                CONTACT_PERSON = worksheet.Cell(row, 3).GetString(),
                                CUSTOMER_CONTACT_NUMBER1 = customerNumber,
                                CUSTOMER_CONTACT_NUMBER2 = worksheet.Cell(row, 5).GetString(),
                                CUSTOMER_CONTACT_NUMBER3 = worksheet.Cell(row, 6).GetString(),
                                COUNTRY = worksheet.Cell(row, 8).GetString(),
                                CITY = worksheet.Cell(row,10 ).GetString(),
                                STATE = worksheet.Cell(row, 9).GetString(),
                                CREATED_BY = "Admin", // Set this based on your logic
                                CREATED_ON = DateTime.Now,
                                MODIFIED_BY = "Admin", // Set this based on your logic
                                MODIFIED_ON = DateTime.Now
                            };

                            customersFromExcel.Add(customer); // Add to the list of valid customers
                        }

                        // Check against the database for existing emails
                        var existingEmails = _context.Customers
                            .Where(c => customersFromExcel.Select(d => d.CUSTOMER_EMAIL.ToLower().Trim()).Contains(c.CUSTOMER_EMAIL.ToLower()))
                            .Select(c => c.CUSTOMER_EMAIL.ToLower())
                            .ToList();

                        // Store records that already exist in the database
                        existingDuplicateRecords = customersFromExcel
                            .Where(c => existingEmails.Contains(c.CUSTOMER_EMAIL.ToLower()))
                            .Select(c => new InvalidCustomerRecord
                            {
                                RowNumber = customersFromExcel.IndexOf(c) + 2, // Adding 2 to adjust for zero-based index and skipping header
                                CustomerName = c.CUSTOMER_NAME,
                                CustomerEmail = c.CUSTOMER_EMAIL,
                                CustomerNumber = c.CUSTOMER_CONTACT_NUMBER1,
                                ErrorMessage = "Email Already Exists in the database."
                            })
                            .ToList();

                        // Filter the customers to only include those not present in the database
                        var newCustomers = customersFromExcel
                            .Where(c => !existingEmails.Contains(c.CUSTOMER_EMAIL.ToLower()))
                            .ToList();

                        // Add new customers to the database
                        if (newCustomers.Count > 0)
                        {
                            try
                            {
                                _context.Customers.AddRange(newCustomers);
                                await _context.SaveChangesAsync();
                            }
                            catch (Exception ex)
                            {
                                // Log the exception message if needed, e.g., using a logging library
                                TempData["ErrorMessage"] = $" {ex.Message}";

                                // Optionally, you could re-throw the exception if you want to handle it further up the chain
                                // throw;
                            }
                        }


                        // Combine invalid records and database duplicates
                        var allInvalidRecords = invalidRecords.Concat(existingDuplicateRecords).ToList();

                        // If there are any invalid or duplicate records, pass them to the view
                        if (allInvalidRecords.Any())
                        {
                            TempData["InvalidRecords"] = JsonConvert.SerializeObject(allInvalidRecords);
                            return View("InvalidRecords", allInvalidRecords);
                        }
                    }

                    TempData["SuccessMessage"] = "Successfully Uploaded";
                    return RedirectToAction(nameof(ViewCustomers));
                }
            }

            TempData["ErrorMessage"] = "File is empty. Please upload a valid Excel file.";
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

        [HttpGet]
        public IActionResult DownloadTemplate()
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("CustomerTemplate");

                // Define the headers in the template.
                worksheet.Cell(1, 1).Value = "CUSTOMER_CODE";
                worksheet.Cell(1, 2).Value = "CUSTOMER_NAME";
                worksheet.Cell(1, 3).Value = "CONTACT_PERSON";
                worksheet.Cell(1, 4).Value = "CUSTOMER_CONTACT_NUMBER1";
                worksheet.Cell(1, 5).Value = "CUSTOMER_CONTACT_NUMBER2";
                worksheet.Cell(1, 6).Value = "CUSTOMER_CONTACT_NUMBER3";
                worksheet.Cell(1, 7).Value = "EMAIL";
                worksheet.Cell(1, 8).Value = "COUNTRY";
                worksheet.Cell(1, 9).Value = "STATE";
                worksheet.Cell(1, 10).Value = "CITY";

                // Optionally, add some example data for user reference (commented out).
                // worksheet.Cell(2, 1).Value = "1001";
                // worksheet.Cell(2, 2).Value = "John Doe";
                // worksheet.Cell(2, 3).Value = "johndoe@example.com";
                // worksheet.Cell(2, 4).Value = "1234567890";
                // worksheet.Cell(2, 5).Value = "USA";
                // worksheet.Cell(2, 6).Value = "New York";
                // worksheet.Cell(2, 7).Value = "New York";

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
                worksheet.Cell(1, 2).Value = "Customer Code";
                worksheet.Cell(1, 3).Value = "Customer Email";
                worksheet.Cell(1, 4).Value = "Error Message";

                // Populating data
                for (int i = 0; i < invalidRecords.Count; i++)
                {
                    var record = invalidRecords[i];
                    worksheet.Cell(i + 2, 1).Value = record.RowNumber;
                    worksheet.Cell(i + 2, 2).Value = record.CustomerName;
                    worksheet.Cell(i + 2, 3).Value = record.CustomerEmail;
                    worksheet.Cell(i + 2, 4).Value = record.ErrorMessage;
                }

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    stream.Position = 0;

                    // Return the Excel file as a downloadable file
                    return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "InvalidRecords.xlsx");
                }
            }
        }



    }
}
