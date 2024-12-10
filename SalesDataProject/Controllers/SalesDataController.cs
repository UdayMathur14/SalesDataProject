using ClosedXML.Excel;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SalesDataProject.Models;
using System.Text.RegularExpressions;

namespace SalesDataProject.Controllers
{
    public class SalesDataController : Controller
    {
        private readonly AppDbContext _context;

        public SalesDataController(AppDbContext context)
        {
            _context = context;
        }


        [HttpGet]
        public IActionResult Index()
        {
            // Check if the user has permission to access the page
            if (HttpContext.Session.GetString("CanAccessSales") != "True")
            {
                // If not authorized, redirect to login page or another appropriate page
                return RedirectToAction("Login", "Auth");
            }

            // Fetch the list of users from the Users master table
            var users = _context.Users.ToList(); // Assuming 'Users' is your DbSet<User> in YourDbContext

            // Pass the list of users to the view using ViewBag
            ViewBag.Users = users;

            // Return the view
            return View();
        }
        public IActionResult UploadResults(UploadResultViewModel model)
        {
            if (HttpContext.Session.GetString("CanAccessSales") != "True")
            {
                // If not authorized, redirect to home or another page
                return RedirectToAction("Login", "Auth");
            }
            return View(model);

        }
        public IActionResult ViewRecords(UploadResultViewModel model)
        {
            if (HttpContext.Session.GetString("CanAccessSales") != "True")
            {
                // If not authorized, redirect to home or another page
                return RedirectToAction("Login", "Auth");
            }
            return View(model);

        }
      

        [HttpPost]
        public async Task<IActionResult> UploadSalesData(IFormFile file, string selectedCategory)
        {
            var username = HttpContext.Session.GetString("Username");
            if (file != null && file.Length > 0)
            {
                var blockedCustomers = new List<ProspectCustomer>();
                var cleanCustomers = new List<ProspectCustomer>();
                var invalidRecords = new List<InvalidCustomerRecord>();

                using (var stream = new MemoryStream())
                {
                    await file.CopyToAsync(stream);
                    using (var workbook = new XLWorkbook(stream))
                    {
                        var worksheet = workbook.Worksheet(1);
                        var lastRow = worksheet.LastRowUsed().RowNumber();

                        for (int row = 3; row <= lastRow; row++) // Start from the third row (skip header)
                        {
                            var companyName = worksheet.Cell(row, 2).GetString();
                            var contactPerson = worksheet.Cell(row, 3).GetString()?.ToUpperInvariant();
                            var customerNumber = worksheet.Cell(row, 4).GetString();
                            var customerEmail = worksheet.Cell(row, 5).GetString()?.ToLowerInvariant();
                            var countryCode = worksheet.Cell(row, 6).GetString()?.Trim();
                            var country = worksheet.Cell(row, 7).GetString();
                            //var category = worksheet.Cell(row, 12).GetString();
                            var emailDomain = customerEmail?.Split('@').Last();

                            if (!IsValidEmail(customerEmail))
                            {
                                // Store invalid record
                                invalidRecords.Add(new InvalidCustomerRecord
                                {
                                    RowNumber = row,
                                    CompanyName = companyName,
                                    CustomerEmail = customerEmail,
                                    CustomerNumber = customerNumber,
                                    ErrorMessage = "Invalid email format."
                                });
                                continue; // Skip to the next row
                            }
                            else if (string.IsNullOrWhiteSpace(companyName) || string.IsNullOrWhiteSpace(customerNumber) ||
                                     string.IsNullOrWhiteSpace(customerEmail) || string.IsNullOrWhiteSpace(countryCode) || string.IsNullOrWhiteSpace(country) )
                            {
                                invalidRecords.Add(new InvalidCustomerRecord
                                {
                                    RowNumber = row,
                                    CompanyName = companyName,
                                    CustomerEmail = customerEmail,
                                    CustomerNumber = customerNumber,
                                    ErrorMessage = "Missing Mandatory Fields"
                                });
                                continue;
                            }

                            // Check if email exists in Customers or Prospects table
                            var existingCustomer = await _context.Customers .Where(c => c.CUSTOMER_EMAIL == customerEmail || c.COMPANY_NAME == companyName).Select(c => c.CREATED_BY).FirstOrDefaultAsync();

                            // Check if email exists in Prospects table
                            var existingProspect = await _context.Prospects.Where(c => c.CUSTOMER_EMAIL == customerEmail ||c.COMPANY_NAME == companyName ||(c.EMAIL_DOMAIN == emailDomain && c.RECORD_TYPE == true)).Select(c => c.CREATED_BY).FirstOrDefaultAsync();


                            var customerData = new ProspectCustomer
                            {
                                CUSTOMER_CODE = worksheet.Cell(row, 1).GetString(),
                                COMPANY_NAME = companyName,
                                CONTACT_PERSON = contactPerson,
                                CUSTOMER_CONTACT_NUMBER1 = customerNumber,
                                CUSTOMER_CONTACT_NUMBER2 = worksheet.Cell(row, 8).GetString(),
                                CUSTOMER_CONTACT_NUMBER3 = worksheet.Cell(row, 9).GetString(),
                                CUSTOMER_EMAIL = customerEmail,
                                COUNTRY = country,
                                STATE = worksheet.Cell(row, 10).GetString(),
                                CITY = worksheet.Cell(row, 11).GetString(),
                                CREATED_ON = DateTime.Now,
                                CREATED_BY = username,
                                MODIFIED_BY = username,
                                MODIFIED_ON = DateTime.Now,
                                COUNTRY_CODE = countryCode,
                                EMAIL_DOMAIN = emailDomain,
                                CATEGORY = selectedCategory,
                            };

                            // Apply blocking logic
                            if (!string.IsNullOrEmpty(existingCustomer) || !string.IsNullOrEmpty(existingProspect))
                            {
                                customerData.RECORD_TYPE = true; // Blocked
                                customerData.IS_EMAIL_BLOCKED = true;

                                // Set BLOCKED_BY to the creator of the existing record
                                customerData.BLOCKED_BY = !string.IsNullOrEmpty(existingCustomer) ? existingCustomer : existingProspect;

                                blockedCustomers.Add(customerData);
                            }
                            else
                            {
                                customerData.RECORD_TYPE = false; // Clean
                                customerData.IS_EMAIL_BLOCKED = false;
                                cleanCustomers.Add(customerData);
                            }

                            _context.Prospects.Add(customerData);
                        }

                        await _context.SaveChangesAsync();
                    }
                }

                var model = new UploadResultViewModel
                {
                    BlockedCustomers = blockedCustomers,
                    CleanCustomers = cleanCustomers,
                    invalidCustomerRecords = invalidRecords
                };
                TempData["Success"] = "Successfully Uploaded";
                return View("UploadResults", model);
            }

            return View();
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
            // Regular expression to match exactly 10 digits
            string pattern = @"^\d{10}$";
            Regex regex = new Regex(pattern);

            // Check if the customer number matches the regex pattern
            return regex.IsMatch(customerNumber);
        }

        [HttpPost]
        public IActionResult ExportToExcel(string BlockedCustomersJson, string CleanCustomersJson, string InvalidCustomersJson)
        {
            var blockedCustomers = JsonConvert.DeserializeObject<List<Customer>>(BlockedCustomersJson);
            var cleanCustomers = JsonConvert.DeserializeObject<List<Customer>>(CleanCustomersJson);
            var invalidCustomers = JsonConvert.DeserializeObject<List<InvalidCustomerRecord>>(InvalidCustomersJson);

            using (var workbook = new XLWorkbook())
            {
                var blockedSheet = workbook.Worksheets.Add("Blocked Customers");
                var cleanSheet = workbook.Worksheets.Add("Clean Customers");
                var invalidSheet = workbook.Worksheets.Add("Invalid Customers");

                // Add headers for blocked customers
                blockedSheet.Cell(1, 1).Value = "Customer Code";
                blockedSheet.Cell(1, 2).Value = "Customer Name";
                blockedSheet.Cell(1, 3).Value = "Email";
                blockedSheet.Cell(1, 4).Value = "Contact Number";

                // Fill data for blocked customers
                for (int i = 0; i < blockedCustomers.Count; i++)
                {
                    blockedSheet.Cell(i + 2, 1).Value = blockedCustomers[i].CUSTOMER_CODE;
                    blockedSheet.Cell(i + 2, 2).Value = blockedCustomers[i].COMPANY_NAME;
                    blockedSheet.Cell(i + 2, 3).Value = blockedCustomers[i].CUSTOMER_EMAIL;
                    blockedSheet.Cell(i + 2, 4).Value = blockedCustomers[i].CUSTOMER_CONTACT_NUMBER1;
                }

                // Add headers for clean customers
                cleanSheet.Cell(1, 1).Value = "Customer Code";
                cleanSheet.Cell(1, 2).Value = "Customer Name";
                cleanSheet.Cell(1, 3).Value = "Email";
                cleanSheet.Cell(1, 4).Value = "Contact Number";

                // Fill data for clean customers
                for (int i = 0; i < cleanCustomers.Count; i++)
                {
                    cleanSheet.Cell(i + 2, 1).Value = cleanCustomers[i].CUSTOMER_CODE;
                    cleanSheet.Cell(i + 2, 2).Value = cleanCustomers[i].COMPANY_NAME;
                    cleanSheet.Cell(i + 2, 3).Value = cleanCustomers[i].CUSTOMER_EMAIL;
                    cleanSheet.Cell(i + 2, 4).Value = cleanCustomers[i].CUSTOMER_CONTACT_NUMBER1;
                }

                invalidSheet.Cell(1, 1).Value = "Row";
                invalidSheet.Cell(1, 2).Value = "Customer Name";
                invalidSheet.Cell(1, 3).Value = "Email";
                invalidSheet.Cell(1, 4).Value = "Contact Number";
                invalidSheet.Cell(1, 5).Value = "Error Message";

                for (int i = 0; i < invalidCustomers.Count; i++)
                {
                    invalidSheet.Cell(i + 2, 1).Value = invalidCustomers[i].RowNumber;
                    invalidSheet.Cell(i + 2, 2).Value = invalidCustomers[i].CompanyName;
                    invalidSheet.Cell(i + 2, 3).Value = invalidCustomers[i].CustomerEmail;
                    invalidSheet.Cell(i + 2, 4).Value = invalidCustomers[i].CustomerNumber;
                    invalidSheet.Cell(i + 2, 5).Value = invalidCustomers[i].ErrorMessage;
                }

                blockedSheet.Columns().AdjustToContents();
                invalidSheet.Columns().AdjustToContents();
                cleanSheet.Columns().AdjustToContents();

                // Optionally, apply styles to the header row for better visibility
                var headerRow = blockedSheet.Range("A1:L1");
                headerRow.Style.Font.Bold = true;
                headerRow.Style.Font.FontColor = XLColor.White;
                headerRow.Style.Fill.BackgroundColor = XLColor.BlueGray;

                var headerRow1 = cleanSheet.Range("A1:L1");
                headerRow1.Style.Font.Bold = true;
                headerRow1.Style.Font.FontColor = XLColor.White;
                headerRow1.Style.Fill.BackgroundColor = XLColor.BlueGray;

                var headerRow2 = invalidSheet.Range("A1:L1");
                headerRow2.Style.Font.Bold = true;
                headerRow2.Style.Font.FontColor = XLColor.White;
                headerRow2.Style.Fill.BackgroundColor = XLColor.BlueGray;
                // Prepare the memory stream to send the Excel file
                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "MailingUploadResults.xlsx");
                }
            }
        }

        [HttpGet]
        public IActionResult DownloadTemplate()
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("CustomerTemplate");

                worksheet.Cell(1, 1).Value = "CustomerCode";
                worksheet.Cell(1, 2).Value = "*CompanyName";
                worksheet.Cell(1, 3).Value = "*ContactPerson";
                worksheet.Cell(1, 4).Value = "*ContactNo1";
                worksheet.Cell(1, 5).Value = "*Email";
                worksheet.Cell(1, 6).Value = "*CountryCode";
                worksheet.Cell(1, 7).Value = "*Country";
                worksheet.Cell(1, 8).Value = "ContactNo2";
                worksheet.Cell(1, 9).Value = "ContactNo3";
                worksheet.Cell(1, 10).Value = "State";
                worksheet.Cell(1, 11).Value = "City";
                //worksheet.Cell(1, 12).Value = "*Category";

                // Example data
                worksheet.Cell(2, 1).Value = "Example(0001)";
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
                //worksheet.Cell(2, 12).Value = "CORPORATE/LAWFIRM/SME/UNIVERSITY";

                // Adjust column widths to fit content
                worksheet.Columns().AdjustToContents();

                // Optionally, apply styles to the header row for better visibility
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
                    return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "MailingTemplate.xlsx");
                }
            }
        }


        [HttpPost]
        public async Task<IActionResult> ViewRecord(UploadResultViewModel model)
        {
            var filteredBlockedCustomers = new List<ProspectCustomer>();
            var filteredCleanCustomers = new List<ProspectCustomer>();
            var username = HttpContext.Session.GetString("Username");
            var category = model.Category;
            //if (model.RecordType == null)
            //{
            //    return View("ViewRecords", model);
            //}

            var filteredCustomers = new List<ProspectCustomer>();

            if (model.RecordType == "Blocked")
            {
                // Fetch blocked customers (RECORD_TYPE = 1) created by the current user, based on the selected date
                filteredCustomers = await _context.Prospects
                    .Where(c => c.RECORD_TYPE == true &&
                                c.CREATED_BY == username && (string.IsNullOrEmpty(category) || c.CATEGORY == category)&&
                                (model.SelectedDate == null ||
                                 (c.CREATED_ON.HasValue && c.CREATED_ON.Value.Date == model.SelectedDate.Value.Date)))
                    .ToListAsync();
                if (!filteredCustomers.Any())
                {
                    TempData["message"] = "No Record Found";
                }
                else
                {
                    TempData["messagesuccess"] = "Successfully Record Found";
                }

                model.BlockedCustomers = filteredCustomers;
            }
            else if (model.RecordType == "Clean")
            {
                // Fetch clean customers (RECORD_TYPE = 0) created by the current user, based on the selected date
                filteredCustomers = await _context.Prospects
                    .Where(c => c.RECORD_TYPE == false &&
                                c.CREATED_BY == username && (string.IsNullOrEmpty(category) || c.CATEGORY == category) &&
                                (model.SelectedDate == null ||
                                 (c.CREATED_ON.HasValue && c.CREATED_ON.Value.Date == model.SelectedDate.Value.Date)))
                    .ToListAsync();
                if (!filteredCustomers.Any())
                {
                    TempData["message"] = "No Record Found";
                }
                else
                {
                    TempData["messagesuccess"] = "Successfully Record Found";
                }
                model.CleanCustomers = filteredCustomers;
            }
            else
            {
                // Fetch both blocked and clean customers created by the current user, based on the selected date
                
                //filteredCustomers = await _context.Prospects
                //    .Where(c => c.CREATED_BY == username &&
                //                (model.SelectedDate == null ||
                //                 (c.CREATED_ON.HasValue && c.CREATED_ON.Value.Date == model.SelectedDate.Value.Date)))
                //    .ToListAsync();
                // model.BlockedCustomers = filteredCustomers; // Uncomment if you want to use this list for both

                //for blocked one 
                filteredBlockedCustomers = await _context.Prospects
                    .Where(c => c.RECORD_TYPE == true &&
                                c.CREATED_BY == username && (string.IsNullOrEmpty(category) || c.CATEGORY == category) &&
                                (model.SelectedDate == null ||
                                 (c.CREATED_ON.HasValue && c.CREATED_ON.Value.Date == model.SelectedDate.Value.Date)))
                    .ToListAsync();
                filteredCleanCustomers = await _context.Prospects
                    .Where(c => c.RECORD_TYPE == false &&
                                c.CREATED_BY == username && (string.IsNullOrEmpty(category) || c.CATEGORY == category) &&
                                (model.SelectedDate == null ||
                                 (c.CREATED_ON.HasValue && c.CREATED_ON.Value.Date == model.SelectedDate.Value.Date)))
                    .ToListAsync();
                if (!filteredBlockedCustomers.Any() && !filteredCleanCustomers.Any())
                {
                    TempData["message"] = "No Record Found";
                }
                else
                {
                    TempData["messagesuccess"] = "Successfully Record Found";
                }
                model.CleanCustomers = filteredCleanCustomers;
                model.BlockedCustomers = filteredBlockedCustomers;
            }
          

            // Populate the view model with the filtered data
            return View("ViewRecords", model);
        }



        [HttpPost]
        public async Task<IActionResult> UpdateCustomerStatus(List<int> BlockedCustomerIds, List<int> CleanCustomerIds)
        {
            if (BlockedCustomerIds.Count == 0 && CleanCustomerIds.Count == 0)
            {
                return RedirectToAction("ViewRecords");
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
                TempData["messagesuccess"] = "Successfully cleaned selected customers.";
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
                TempData["messagesuccess"] = "Successfully blocked selected customers.";

            }

            // Redirect back to the ViewEmailRecords action with the selected RecordType and SelectedDate
            return RedirectToAction("ViewRecords"); // Adjust as needed
        }

        [HttpPost]
        public async Task<IActionResult> ViewEmailRecords(string RecordType, DateTime? SelectedDate,string category)
        {
            var username = HttpContext.Session.GetString("Username");

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

            // If both record type and selected date are not provided
            if (string.IsNullOrEmpty(RecordType) && !SelectedDate.HasValue)
            {
                return View("ViewRecords", model); // Pass the empty model to the view
            }

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

                if(model.BlockCustomersEmailList.Any() || model.CleanCustomersEmailList.Any())
                {
                    TempData["messagesuccess"] = "Records found Successfully";
                }
                else
                {
                    TempData["message"] = "No Record found";
                }

            }

            return View("ViewRecords", model); // Return the view with the populated UploadResultViewModel
        }

    }
}
