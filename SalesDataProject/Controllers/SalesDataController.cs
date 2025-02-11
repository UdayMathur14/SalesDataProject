using ClosedXML.Excel;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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
        public async Task<IActionResult> Index()
        {
            try
            {
                var canAccessCustomer = HttpContext.Session.GetString("CanAccessCustomer");
                if (canAccessCustomer != "True")
                {
                    // If not authorized, redirect to login page
                    return RedirectToAction("Login", "Auth");
                }
                // Fetch the users from the database
                return View();
            }
            catch (Exception ex)
            {
                return RedirectToAction("Login", "Auth");
            }
        }


        public IActionResult UploadResults(UploadResultViewModel model)
        {
            try
            {
                if (HttpContext.Session.GetString("CanAccessSales") != "True")
                {
                    // If not authorized, redirect to home or another page
                    return RedirectToAction("Login", "Auth");
                }
                return View(model);
            }
            catch (Exception ex)
            {
                return RedirectToAction("Login", "Auth");
            }
        }

        public async Task<IActionResult> ViewRecords(UploadResultViewModel model)
        {
            var username = HttpContext.Session.GetString("Username");
            try
            {
                if (HttpContext.Session.GetString("CanAccessSales") != "True")
                {
                    // If not authorized, redirect to home or another page
                    return RedirectToAction("Login", "Auth");
                }
                var eventNames = _context.Prospects
            .Where(pc => pc.CREATED_BY == username && !string.IsNullOrEmpty(pc.EVENT_NAME))
            .Select(pc => pc.EVENT_NAME)
            .Distinct()
            .ToList();

                ViewBag.EventNames = new SelectList(eventNames);
                var users = await _context.Users.ToListAsync();

                // Pass the list of users to the view using ViewBag
                ViewBag.Users = new SelectList(users, "Username", "Username");

                return View(model);
            }
            catch (Exception ex)
            {
                return RedirectToAction("Login", "Auth");
            }
        }


        [HttpPost]
        public async Task<IActionResult> UploadSalesData(IFormFile file, string selectedCategory)
        {
            try
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

                            var emailSet = new HashSet<string>();
                            var duplicateEmails = new HashSet<string>();

                            for (int row = 3; row <= lastRow; row++) // Start from the third row (skip header)
                            {
                                var companyName = worksheet.Cell(row, 2).GetString().Trim().ToUpper();
                                var contactPerson = worksheet.Cell(row, 3).GetString();
                                var customerNumber = worksheet.Cell(row, 4).GetString();
                                var customerNumber2 = worksheet.Cell(row, 8).GetString();
                                var customerNumber3 = worksheet.Cell(row, 9).GetString();
                                var customerEmail = worksheet.Cell(row, 5).GetString()?.ToLowerInvariant();
                                var countryCode = worksheet.Cell(row, 6).GetString()?.Trim();
                                var country = worksheet.Cell(row, 7).GetString();
                                var category = worksheet.Cell(row, 12).GetString().ToUpper().Trim();
                                var emailDomain = customerEmail?.Split('@').Last().ToLower();

                                var isCommonDomain = await _context.CommonDomains
                                    .AnyAsync(d => d.DomainName.ToLower() == emailDomain);

                                if (isCommonDomain)
                                {
                                    emailDomain = "NULL"; // Set to null if it is a common domain
                                }
                                if (!string.IsNullOrWhiteSpace(customerEmail))
                                {
                                    if (emailSet.Contains(customerEmail))
                                    {
                                        duplicateEmails.Add(customerEmail); // Mark as duplicate
                                    }
                                    else
                                    {
                                        emailSet.Add(customerEmail); // Add to the set
                                    }
                                }

                                if (!new[] { "CORPORATE", "LAWFIRM", "UNIVERSITY", "PCT", "SME", "LAW FIRM" }.Contains(category?.ToUpperInvariant()))
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
                                if ((!IsValidPhoneNumber(customerNumber) || !IsValidPhoneNumber(customerNumber2) || !IsValidPhoneNumber(customerNumber3)) && !string.IsNullOrWhiteSpace(customerNumber))
                                {
                                    invalidRecords.Add(new InvalidCustomerRecord
                                    {
                                        RowNumber = row,
                                        CompanyName = companyName,
                                        CustomerEmail = customerEmail,
                                        CustomerNumber = customerNumber,
                                        ErrorMessage = "Invalid Contact Number."
                                    });
                                    continue;
                                }
                                if (!IsValidEmail(customerEmail) || duplicateEmails.Contains(customerEmail))
                                {
                                    invalidRecords.Add(new InvalidCustomerRecord
                                    {
                                        RowNumber = row,
                                        CompanyName = companyName,
                                        CustomerEmail = customerEmail,
                                        CustomerNumber = customerNumber,
                                        ErrorMessage = duplicateEmails.Contains(customerEmail) ? "Duplicate email within the file." : "Invalid email format."
                                    });
                                    continue;
                                }
                                else if (string.IsNullOrWhiteSpace(companyName) ||
                                         string.IsNullOrWhiteSpace(customerEmail) || string.IsNullOrWhiteSpace(countryCode) || string.IsNullOrWhiteSpace(country))
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
                                bool isAlreadyUploadedByOther = false;

                                var isAlreadyInMaster = await _context.Customers.Where(c => c.COMPANY_NAME.ToUpper() == companyName.ToUpper() || c.CUSTOMER_EMAIL.ToLower() == customerEmail.ToLower() || c.EMAIL_DOMAIN.ToLower() == emailDomain.ToLower()).AnyAsync();
                                if (emailDomain == "NULL")
                                {
                                    isAlreadyUploadedByOther = await _context.Prospects.Where(c => ((c.COMPANY_NAME.ToUpper() == companyName.ToUpper() || c.CUSTOMER_EMAIL.ToLower() == customerEmail.ToLower()) && c.CREATED_BY != username)).AnyAsync();
                                }
                                else
                                {
                                    isAlreadyUploadedByOther = await _context.Prospects.Where(c => ((c.COMPANY_NAME.ToUpper() == companyName.ToUpper() || c.EMAIL_DOMAIN.ToLower() == emailDomain.ToLower() || c.CUSTOMER_EMAIL.ToLower() == customerEmail.ToLower()) && c.CREATED_BY != username)).AnyAsync();
                                }

                                var isAlreadyUploadedBySameOrOther = await _context.Prospects.Where(c => c.CUSTOMER_EMAIL.ToLower() == customerEmail.ToLower()).AnyAsync();

                                // New logic: Check if record type is true in the Prospects table
                                var isBlockedInProspectTable = await _context.Prospects
                                    .Where(c => c.RECORD_TYPE == true &&
                                                (c.COMPANY_NAME.ToUpper() == companyName.ToUpper() ||
                                                 c.EMAIL_DOMAIN.ToLower() == emailDomain.ToLower()
                                                 || c.CUSTOMER_EMAIL.ToLower() == customerEmail.ToLower()))
                                    .AnyAsync();

                                var customerData = new ProspectCustomer
                                {
                                    CUSTOMER_CODE = "1",
                                    COMPANY_NAME = companyName,
                                    CONTACT_PERSON = contactPerson,
                                    CUSTOMER_CONTACT_NUMBER1 = customerNumber,
                                    CUSTOMER_CONTACT_NUMBER2 = customerNumber2,
                                    CUSTOMER_CONTACT_NUMBER3 = customerNumber3,
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
                                    CATEGORY = category,
                                };

                                // Apply blocking logic
                                if (isAlreadyUploadedByOther || isBlockedInProspectTable || isAlreadyUploadedBySameOrOther)
                                {
                                    customerData.RECORD_TYPE = true; // Blocked
                                    customerData.BLOCKED_BY = "Another User";
                                    blockedCustomers.Add(customerData);
                                }
                                else
                                {
                                    customerData.RECORD_TYPE = false; // Clean
                                    cleanCustomers.Add(customerData);
                                    _context.Prospects.Add(customerData);
                                }
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
                    TempData["Message"] = "Successfully Uploaded";
                    TempData["MessageType"] = "Success";
                    return View("UploadResults", model);
                }
                return View();
            }
            catch (Exception ex)
            {
                var model = new UploadResultViewModel
                {

                };
                TempData["Message"] = "An unexpected error occurred. Please try again.";
                TempData["MessageType"] = "Error";
                return View("ViewRecords", model);
            }
        }



        [HttpPost]
        public async Task<IActionResult> UploadSalesDataEvent(IFormFile file, string eventName, DateTime eventDate)
        {
            try
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

                            var emailSet = new HashSet<string>();
                            var duplicateEmails = new HashSet<string>();

                            for (int row = 3; row <= lastRow; row++) // Start from the third row (skip header)
                            {
                                var companyName = worksheet.Cell(row, 2).GetString().Trim().ToUpper();
                                var contactPerson = worksheet.Cell(row, 3).GetString();
                                var customerNumber = worksheet.Cell(row, 4).GetString();
                                var customerNumber2 = worksheet.Cell(row, 8).GetString();
                                var customerNumber3 = worksheet.Cell(row, 9).GetString();
                                var customerEmail = worksheet.Cell(row, 5).GetString()?.ToLowerInvariant();
                                var countryCode = worksheet.Cell(row, 6).GetString()?.Trim();
                                var country = worksheet.Cell(row, 7).GetString();
                                var category = worksheet.Cell(row, 12).GetString().ToUpper().Trim();
                                var emailDomain = customerEmail?.Split('@').Last().ToLower();

                                var isCommonDomain = await _context.CommonDomains
                                    .AnyAsync(d => d.DomainName.ToLower() == emailDomain);

                                if (isCommonDomain)
                                {
                                    emailDomain = "NULL"; // Set to null if it is a common domain
                                }
                                if (!string.IsNullOrWhiteSpace(customerEmail))
                                {
                                    if (emailSet.Contains(customerEmail))
                                    {
                                        duplicateEmails.Add(customerEmail); // Mark as duplicate
                                    }
                                    else
                                    {
                                        emailSet.Add(customerEmail); // Add to the set
                                    }
                                }

                                if (!new[] { "CORPORATE", "LAWFIRM", "UNIVERSITY", "PCT", "SME", "LAW FIRM" }.Contains(category?.ToUpperInvariant()))
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
                                if ((!IsValidPhoneNumber(customerNumber) || !IsValidPhoneNumber(customerNumber2) || !IsValidPhoneNumber(customerNumber3)) && !string.IsNullOrWhiteSpace(customerNumber))
                                {
                                    invalidRecords.Add(new InvalidCustomerRecord
                                    {
                                        RowNumber = row,
                                        CompanyName = companyName,
                                        CustomerEmail = customerEmail,
                                        CustomerNumber = customerNumber,
                                        ErrorMessage = "Invalid Contact Number."
                                    });
                                    continue;
                                }
                                if (!IsValidEmail(customerEmail) || duplicateEmails.Contains(customerEmail))
                                {
                                    invalidRecords.Add(new InvalidCustomerRecord
                                    {
                                        RowNumber = row,
                                        CompanyName = companyName,
                                        CustomerEmail = customerEmail,
                                        CustomerNumber = customerNumber,
                                        ErrorMessage = duplicateEmails.Contains(customerEmail) ? "Duplicate email within the file." : "Invalid email format."
                                    });
                                    continue;
                                }
                                else if (string.IsNullOrWhiteSpace(companyName) ||
                                         string.IsNullOrWhiteSpace(customerEmail) || string.IsNullOrWhiteSpace(countryCode) || string.IsNullOrWhiteSpace(country))
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
                                bool isAlreadyUploadedByOther = false;

                                var isAlreadyInMaster = await _context.Customers.Where(c => c.COMPANY_NAME.ToUpper() == companyName.ToUpper() || c.CUSTOMER_EMAIL.ToLower() == customerEmail.ToLower() || c.EMAIL_DOMAIN.ToLower() == emailDomain.ToLower()).AnyAsync();
                                if (emailDomain == "NULL")
                                {
                                    isAlreadyUploadedByOther = await _context.Prospects.Where(c => ((c.COMPANY_NAME.ToUpper() == companyName.ToUpper() || c.CUSTOMER_EMAIL.ToLower() == customerEmail.ToLower()) && c.CREATED_BY != username)).AnyAsync();
                                }
                                else
                                {
                                    isAlreadyUploadedByOther = await _context.Prospects.Where(c => ((c.COMPANY_NAME.ToUpper() == companyName.ToUpper() || c.EMAIL_DOMAIN.ToLower() == emailDomain.ToLower() || c.CUSTOMER_EMAIL.ToLower() == customerEmail.ToLower()) && c.CREATED_BY != username)).AnyAsync();
                                }

                                var isAlreadyUploadedBySameOrOther = await _context.Prospects.Where(c => c.CUSTOMER_EMAIL.ToLower() == customerEmail.ToLower()).AnyAsync();

                                // New logic: Check if record type is true in the Prospects table
                                var isBlockedInProspectTable = await _context.Prospects
                                    .Where(c => c.RECORD_TYPE == true &&
                                                (c.COMPANY_NAME.ToUpper() == companyName.ToUpper() ||
                                                 c.EMAIL_DOMAIN.ToLower() == emailDomain.ToLower()
                                                 || c.CUSTOMER_EMAIL.ToLower() == customerEmail.ToLower()))
                                    .AnyAsync();

                                var customerData = new ProspectCustomer
                                {
                                    CUSTOMER_CODE = "1",
                                    COMPANY_NAME = companyName,
                                    CONTACT_PERSON = contactPerson,
                                    CUSTOMER_CONTACT_NUMBER1 = customerNumber,
                                    CUSTOMER_CONTACT_NUMBER2 = customerNumber2,
                                    CUSTOMER_CONTACT_NUMBER3 = customerNumber3,
                                    CUSTOMER_EMAIL = customerEmail,
                                    COUNTRY = country,
                                    STATE = worksheet.Cell(row, 10).GetString(),
                                    CITY = worksheet.Cell(row, 11).GetString(),
                                    CREATED_ON = eventDate,
                                    CREATED_BY = username,
                                    MODIFIED_BY = username,
                                    MODIFIED_ON = DateTime.Now,
                                    COUNTRY_CODE = countryCode,
                                    EMAIL_DOMAIN = emailDomain,
                                    CATEGORY = category,
                                    EVENT_NAME = eventName,
                                };

                                // Apply blocking logic
                                if (isAlreadyUploadedByOther || isBlockedInProspectTable || isAlreadyUploadedBySameOrOther)
                                {
                                    customerData.RECORD_TYPE = true; // Blocked
                                    customerData.BLOCKED_BY = "Another User";
                                    blockedCustomers.Add(customerData);
                                }
                                else
                                {
                                    customerData.RECORD_TYPE = false; // Clean
                                    cleanCustomers.Add(customerData);
                                    _context.Prospects.Add(customerData);
                                }
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
                    TempData["Message"] = "Successfully Uploaded";
                    TempData["MessageType"] = "Success";
                    return View("UploadResults", model);
                }

                return View();
            }
            catch (Exception ex)
            {
                var model = new UploadResultViewModel
                {

                };
                TempData["Message"] = "An unexpected error occurred. Please try again.";
                TempData["MessageType"] = "Error";
                return View("ViewRecords", model);
            }
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

        [HttpPost]
        public IActionResult ExportToExcel(string BlockedCustomersJson, string CleanCustomersJson, string InvalidCustomersJson)
        {
            try
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
                    blockedSheet.Cell(1, 2).Value = "Company Name";
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
                    cleanSheet.Cell(1, 2).Value = "Company Name";
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
                    invalidSheet.Cell(1, 2).Value = "Company Name";
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
            catch (Exception ex)
            {
                TempData["Message"] = "An unexpected error occurred. Please try again.";
                TempData["MessageType"] = "Error";
                return View("ViewRecords");
            }
        }

        [HttpGet]
        public IActionResult DownloadTemplate()
        {
            try
            {
                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("CustomerTemplate");

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
            catch (Exception ex)
            {
                TempData["Message"] = "An unexpected error occurred. Please try again.";
                TempData["MessageType"] = "Error";
                return View("ViewRecords");
            }
        }

        [HttpGet]
        public IActionResult DownloadTemplate1()
        {
            try
            {
                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("EventTemplate");

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
                        
                        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "EventTemplate.xlsx");
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["Message"] = "An unexpected error occurred. Please try again.";
                TempData["MessageType"] = "Error";
                return View("ViewRecords");
            }
        }


        [HttpPost]
        public async Task<IActionResult> ViewRecord(UploadResultViewModel model)
        {
            try
            {
                var filteredBlockedCustomers = new List<ProspectCustomer>();
                var filteredCleanCustomers = new List<ProspectCustomer>();
                var username = HttpContext.Session.GetString("Username");
                var category = model.Category;
                var eventName = model.Event; // Get selected Event Name from model

                var filteredCustomers = new List<ProspectCustomer>();

                if (model.RecordType == "Blocked")
                {
                    filteredCustomers = await _context.Prospects
                        .Where(c => c.RECORD_TYPE == true &&
                                    c.CREATED_BY == username &&
                                    (string.IsNullOrEmpty(category) || c.CATEGORY == category) &&
                                    (string.IsNullOrEmpty(eventName) || c.EVENT_NAME == eventName) && // Event filter
                                    (model.SelectedDate == null ||
                                     (c.CREATED_ON.HasValue && c.CREATED_ON.Value.Date == model.SelectedDate.Value.Date)))
                        .ToListAsync();

                    TempData["Message"] = filteredCustomers.Any() ? "Successfully Record Found" : "No Record Found";
                    
                    TempData["MessageType"] = "Success";

                    model.BlockedCustomers = filteredCustomers;
                }
                else if (model.RecordType == "Clean")
                {
                    filteredCustomers = await _context.Prospects
                        .Where(c => c.RECORD_TYPE == false &&
                                    c.CREATED_BY == username &&
                                    (string.IsNullOrEmpty(category) || c.CATEGORY == category) &&
                                    (string.IsNullOrEmpty(eventName) || c.EVENT_NAME == eventName) && // Event filter
                                    (model.SelectedDate == null ||
                                     (c.CREATED_ON.HasValue && c.CREATED_ON.Value.Date == model.SelectedDate.Value.Date)))
                        .ToListAsync();

                    TempData["Message"] = filteredCustomers.Any() ? "Successfully Record Found" : "No Record Found";
                    
                    TempData["MessageType"] = "Success";

                    model.CleanCustomers = filteredCustomers;
                }
                else
                {
                    filteredBlockedCustomers = await _context.Prospects
                        .Where(c => c.RECORD_TYPE == true &&
                                    c.CREATED_BY == username &&
                                    (string.IsNullOrEmpty(category) || c.CATEGORY == category) &&
                                    (string.IsNullOrEmpty(eventName) || c.EVENT_NAME == eventName) && // Event filter
                                    (model.SelectedDate == null ||
                                     (c.CREATED_ON.HasValue && c.CREATED_ON.Value.Date == model.SelectedDate.Value.Date)))
                        .ToListAsync();

                    filteredCleanCustomers = await _context.Prospects
                        .Where(c => c.RECORD_TYPE == false &&
                                    c.CREATED_BY == username &&
                                    (string.IsNullOrEmpty(category) || c.CATEGORY == category) &&
                                    (string.IsNullOrEmpty(eventName) || c.EVENT_NAME == eventName) && // Event filter
                                    (model.SelectedDate == null ||
                                     (c.CREATED_ON.HasValue && c.CREATED_ON.Value.Date == model.SelectedDate.Value.Date)))
                        .ToListAsync();

                    TempData["Message"] = (filteredBlockedCustomers.Any() || filteredCleanCustomers.Any())
                                            ? "Successfully Record Found"
                                            : "No Record Found";
                    
                    TempData["MessageType"] = "Success";

                    model.CleanCustomers = filteredCleanCustomers;
                    model.BlockedCustomers = filteredBlockedCustomers;
                }

                return View("ViewRecords", model);
            }
            catch (Exception ex)
            {
                TempData["Message"] = "An unexpected error occurred. Please try again.";
                TempData["MessageType"] = "Error";
                return View("ViewRecords", model);
            }
        }
    }
}
