using ClosedXML.Excel;
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
            return View();
        }
        public async Task<IActionResult> ViewCustomers(Customer model)
        {
            var Customers = await _context.Customers.ToListAsync();
            return View(Customers);

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
        [HttpPost]
        public async Task<IActionResult> UploadExcel(IFormFile file)
        {
            var invalidRecords = new List<InvalidCustomerRecord>(); // List to store invalid records

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
                            var customerEmail = worksheet.Cell(row, 3).GetString();
                            var customerCode = worksheet.Cell(row, 1).GetString();

                            // Validate email format
                            if (!IsValidEmail(customerEmail))
                            {
                                // Store invalid record
                                invalidRecords.Add(new InvalidCustomerRecord
                                {
                                    RowNumber = row,
                                    CustomerCode = customerCode,
                                    CustomerEmail = customerEmail,
                                    ErrorMessage = "Invalid email format."
                                });
                                continue; // Skip to the next row
                            }

                            // Create a customer object
                            var customer = new Customer
                            {
                                CUSTOMER_CODE = customerCode,
                                CUSTOMER_NAME = worksheet.Cell(row, 2).GetString(),
                                CUSTOMER_EMAIL = customerEmail,
                                CUSTOMER_CONTACT_NUMBER = worksheet.Cell(row, 4).GetString(),
                                COUNTRY = worksheet.Cell(row, 5).GetString(),
                                CITY = worksheet.Cell(row, 6).GetString(),
                                STATE = worksheet.Cell(row, 7).GetString(),
                                CREATED_BY = "Admin", // Set this based on your logic
                                CREATED_ON = DateTime.Now,
                                MODIFIED_BY = "Admin", // Set this based on your logic
                                MODIFIED_ON = DateTime.Now
                            };

                            // Check for duplicates in the list of customers
                            if (customersFromExcel.Any(c => c.CUSTOMER_EMAIL.ToLower().Trim() == customerEmail.ToLower().Trim()))
                            {
                                // Store duplicate record
                                invalidRecords.Add(new InvalidCustomerRecord
                                {
                                    RowNumber = row,
                                    CustomerCode = customerCode,
                                    CustomerEmail = customerEmail,
                                    ErrorMessage = "Duplicate email in the uploaded file."
                                });
                                continue; // Skip to the next row
                            }

                            customersFromExcel.Add(customer); // Add to the list of valid customers
                        }

                        // Check against the database for existing emails
                        var existingEmails = _context.Customers
                            .Where(c => customersFromExcel.Select(d => d.CUSTOMER_EMAIL).Contains(c.CUSTOMER_EMAIL))
                            .Select(c => c.CUSTOMER_EMAIL.ToLower())
                            .ToList();

                        // Filter the distinct customers to only include those not present in the database
                        var newCustomers = customersFromExcel
                            .Where(c => !existingEmails.Contains(c.CUSTOMER_EMAIL.ToLower()))
                            .ToList();

                        // Add new customers to the database
                        if (newCustomers.Count > 0)
                        {
                            _context.Customers.AddRange(newCustomers);
                            await _context.SaveChangesAsync();
                        }

                        // If there are invalid records, set them to TempData for the view
                        if (invalidRecords.Any())
                        {
                            TempData["InvalidRecords"] = JsonConvert.SerializeObject(invalidRecords);
                        }
                    }

                    TempData["SuccessMessage"] = "Successfully Uploaded";
                    return RedirectToAction(nameof(Index));
                }
            }

            TempData["ErrorMessage"] = "File is empty. Please upload a valid Excel file.";
            return RedirectToAction(nameof(Index));
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
                worksheet.Cell(1, 3).Value = "CUSTOMER_EMAIL";
                worksheet.Cell(1, 4).Value = "CUSTOMER_CONTACT_NUMBER";
                worksheet.Cell(1, 5).Value = "COUNTRY";
                worksheet.Cell(1, 6).Value = "CITY";
                worksheet.Cell(1, 7).Value = "STATE";

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

        [HttpPost]
        public IActionResult ExportInvalidRecords(string invalidRecordsJson)
        {
            var invalidRecords = JsonConvert.DeserializeObject<List<InvalidCustomerRecord>>(invalidRecordsJson);

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Invalid Records");
                worksheet.Cell(1, 1).Value = "Row Number";
                worksheet.Cell(1, 2).Value = "Customer Code";
                worksheet.Cell(1, 3).Value = "Email";
                worksheet.Cell(1, 4).Value = "Error Message";

                for (int i = 0; i < invalidRecords.Count; i++)
                {
                    var record = invalidRecords[i];
                    worksheet.Cell(i + 2, 1).Value = record.RowNumber;
                    worksheet.Cell(i + 2, 2).Value = record.CustomerCode;
                    worksheet.Cell(i + 2, 3).Value = record.CustomerEmail;
                    worksheet.Cell(i + 2, 4).Value = record.ErrorMessage;
                }

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var fileName = "InvalidRecords.xlsx";
                    return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                }
            }
        }


    }
}
