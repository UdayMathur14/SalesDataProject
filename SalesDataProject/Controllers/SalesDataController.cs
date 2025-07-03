using ClosedXML.Excel;
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
                var eventNames = _context.CleanProspects
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

                if(string.IsNullOrWhiteSpace(username)|| username==null || username=="")
                {
                    TempData["Message"] = "User not logged in.";
                    TempData["MessageType"] = "Error";
                    return RedirectToAction("Login", "Auth");
                }

                if (file == null || file.Length == 0)
                {
                    TempData["Message"] = "No file uploaded.";
                    TempData["MessageType"] = "Error";
                    return View("ViewRecords", new UploadResultViewModel());
                }

                var cleanCustomers = new List<ProspectCustomerClean>();
                var blockedCustomers = new List<ProspectCustomerBlocked>();
                var invalidRecords = new List<InvalidCustomerRecord>();
                var emailSet = new HashSet<string>();

                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);
                using var workbook = new XLWorkbook(stream);

                var worksheet = workbook.Worksheet(1);
                var lastRow = worksheet.LastRowUsed().RowNumber();
                var validCategories = new[] { "CORPORATE", "LAWFIRM", "UNIVERSITY", "PCT", "MSME", "LAW FIRM", "INDIVIDUAL" };

                for (int row = 2; row <= lastRow; row++)
                {
                    var companyName = worksheet.Cell(row, 1).GetString().Trim().ToUpper();
                    var contactPerson = worksheet.Cell(row, 2).GetString().Trim().ToUpper();
                    var customerNumber1 = worksheet.Cell(row, 3).GetString().Trim();
                    var customerEmail = worksheet.Cell(row, 4).GetString().Trim().ToLowerInvariant();
                    var emailDomain = customerEmail.Contains('@') ? customerEmail.Split('@').Last() : "";
                    var countryCode = worksheet.Cell(row, 5).GetString().Trim();
                    var country = worksheet.Cell(row, 6).GetString().Trim();
                    var customerNumber2 = worksheet.Cell(row, 7).GetString().Trim();
                    var customerNumber3 = worksheet.Cell(row, 8).GetString().Trim();
                    var state = worksheet.Cell(row, 9).GetString().Trim();
                    var city = worksheet.Cell(row, 10).GetString().Trim();
                    var category = worksheet.Cell(row, 11).GetString().Trim().ToUpper();

                    bool isCommonDomain = await _context.CommonDomains.AnyAsync(x => x.DomainName.ToLower() == emailDomain);
                    if (isCommonDomain) emailDomain = null;

                    if (!validCategories.Contains(category))
                    {
                        invalidRecords.Add(new InvalidCustomerRecord { RowNumber = row, CompanyName = companyName, CustomerEmail = customerEmail, CustomerNumber = customerNumber1, ErrorMessage = "Invalid category." });
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(companyName) || string.IsNullOrWhiteSpace(countryCode) || string.IsNullOrWhiteSpace(country))
                    {
                        invalidRecords.Add(new InvalidCustomerRecord { RowNumber = row, CompanyName = companyName, CustomerEmail = customerEmail, CustomerNumber = customerNumber1, ErrorMessage = "Missing mandatory fields." });
                        continue;
                    }

                    bool isEmailEmpty = string.IsNullOrWhiteSpace(customerEmail);
                    bool isAllContactsEmpty = string.IsNullOrWhiteSpace(customerNumber1) ;

                    if (isEmailEmpty && isAllContactsEmpty)
                    {
                        invalidRecords.Add(new InvalidCustomerRecord { RowNumber = row, CompanyName = companyName, CustomerEmail = customerEmail, CustomerNumber = customerNumber1, ErrorMessage = "Email or contact number required." });
                        continue;
                    }

                    if (!isEmailEmpty && (!IsValidEmail(customerEmail) || !emailSet.Add(customerEmail)))
                    {
                        invalidRecords.Add(new InvalidCustomerRecord { RowNumber = row, CompanyName = companyName, CustomerEmail = customerEmail, CustomerNumber = customerNumber1, ErrorMessage = !IsValidEmail(customerEmail) ? "Invalid email." : "Duplicate email in Excel File" });
                        continue;
                    }


                    if (!isEmailEmpty && !isAllContactsEmpty)
                    {
                        var clean = await _context.CleanProspects.FirstOrDefaultAsync(x => x.COMPANY_NAME == companyName && x.CONTACT_PERSON == contactPerson &&
                                 (string.IsNullOrWhiteSpace(customerEmail) || x.CUSTOMER_EMAIL == customerEmail) &&
                                 (string.IsNullOrWhiteSpace(customerNumber1) || x.CUSTOMER_CONTACT_NUMBER1 == customerNumber1));

                        var blocked = await _context.BlockedProspects.FirstOrDefaultAsync(x =>
                            x.COMPANY_NAME == companyName &&
                            x.CONTACT_PERSON == contactPerson &&
                            (string.IsNullOrWhiteSpace(customerEmail) || x.CUSTOMER_EMAIL == customerEmail) &&
                            (string.IsNullOrWhiteSpace(customerNumber1) || x.CUSTOMER_CONTACT_NUMBER1 == customerNumber1));

                        if (clean != null || blocked != null)
                        {
                            var matchedBy = clean?.CREATED_BY ?? blocked?.CREATED_BY ?? "Unknown";

                            blockedCustomers.Add(new ProspectCustomerBlocked
                            {
                                COMPANY_NAME = companyName,
                                CONTACT_PERSON = contactPerson,
                                CUSTOMER_CONTACT_NUMBER1 = customerNumber1,
                                CUSTOMER_CONTACT_NUMBER2 = customerNumber2,
                                CUSTOMER_CONTACT_NUMBER3 = customerNumber3,
                                CUSTOMER_EMAIL = customerEmail,
                                EMAIL_DOMAIN = emailDomain,
                                COUNTRY = country,
                                COUNTRY_CODE = countryCode,
                                STATE = state,
                                CITY = city,
                                CATEGORY = category,
                                CREATED_BY = username,
                                CREATED_ON = DateTime.UtcNow,
                                BLOCKED_BY = matchedBy,
                                BLOCK_REASON = $"Same Details already uploaded by '{matchedBy}'"
                            });
                            continue;
                        }
                    }

                    string blockedReason = "";
                    string blockedByName = "";

                    List<string> reasons = new List<string>();

                    var cleanMatch = await _context.CleanProspects.FirstOrDefaultAsync(x =>
                        (!string.IsNullOrWhiteSpace(customerEmail) && x.CUSTOMER_EMAIL.ToLower() == customerEmail.ToLower()) ||
                        (!string.IsNullOrWhiteSpace(customerNumber1) && x.CUSTOMER_CONTACT_NUMBER1 == customerNumber1) ||
                        (!string.IsNullOrWhiteSpace(emailDomain) && x.EMAIL_DOMAIN.ToLower() == emailDomain.ToLower()) ||
                        (!string.IsNullOrWhiteSpace(companyName) && x.COMPANY_NAME.ToLower().Contains(companyName.Substring(0, Math.Max(2, companyName.Length / 2)).ToLower())) ||
                        ((!string.IsNullOrWhiteSpace(companyName) && !string.IsNullOrWhiteSpace(contactPerson)) &&
                         (x.COMPANY_NAME + x.CONTACT_PERSON).ToLower().Contains((companyName + contactPerson).Substring(0, Math.Max(3, (companyName + contactPerson).Length / 2)).ToLower()))
                    );

                    if (cleanMatch != null && cleanMatch.CREATED_BY!=username)
                    {
                        blockedByName = cleanMatch.CREATED_BY;



                        if (!string.IsNullOrWhiteSpace(customerEmail) && cleanMatch.CUSTOMER_EMAIL.ToLower() == customerEmail.ToLower())
                            reasons.Add("Email");

                        if (!string.IsNullOrWhiteSpace(customerNumber1) && cleanMatch.CUSTOMER_CONTACT_NUMBER1 == customerNumber1)
                            reasons.Add("Contact Number");

                        if (!string.IsNullOrWhiteSpace(emailDomain) && !string.IsNullOrWhiteSpace(cleanMatch.EMAIL_DOMAIN) && cleanMatch.EMAIL_DOMAIN.ToLower() == emailDomain.ToLower())
                            reasons.Add("Domain");

                        if (!string.IsNullOrWhiteSpace(companyName) && cleanMatch.COMPANY_NAME.ToLower().Contains(companyName.Substring(0, Math.Max(2, companyName.Length / 2)).ToLower()))
                            reasons.Add("Company Name");

                        blockedReason = string.Join(", ", reasons); // e.g., "Email, Domain, Company"
                    }

                    else
                    {
                        var blockMatch = await _context.BlockedProspects.FirstOrDefaultAsync(x =>
                            (!string.IsNullOrWhiteSpace(customerEmail) && x.CUSTOMER_EMAIL.ToLower() == customerEmail.ToLower()) &&
                            (!string.IsNullOrWhiteSpace(customerNumber1) && x.CUSTOMER_CONTACT_NUMBER1 == customerNumber1) ||
                            (!string.IsNullOrWhiteSpace(emailDomain) && x.EMAIL_DOMAIN.ToLower() == emailDomain.ToLower()) &&
                            (!string.IsNullOrWhiteSpace(companyName) && x.COMPANY_NAME.ToLower().Contains(companyName.Substring(0, Math.Max(2, companyName.Length / 2)).ToLower())) &&
                            ((!string.IsNullOrWhiteSpace(companyName) && !string.IsNullOrWhiteSpace(contactPerson)) && (x.COMPANY_NAME + x.CONTACT_PERSON).ToLower().Contains((companyName + contactPerson).Substring(0, Math.Max(3, (companyName + contactPerson).Length / 2)).ToLower())));

                        if (blockMatch != null)
                        {
                            if (!string.IsNullOrWhiteSpace(customerEmail) && blockMatch.CUSTOMER_EMAIL.ToLower() == customerEmail.ToLower())
                                reasons.Add("Email");

                            if (!string.IsNullOrWhiteSpace(customerNumber1) && blockMatch.CUSTOMER_CONTACT_NUMBER1 == customerNumber1)
                                reasons.Add("Contact Number");

                            if (!string.IsNullOrWhiteSpace(emailDomain) && blockMatch.EMAIL_DOMAIN.ToLower() == emailDomain.ToLower())
                                reasons.Add("Domain");

                            if (!string.IsNullOrWhiteSpace(companyName) && blockMatch.COMPANY_NAME.ToLower().Contains(companyName.Substring(0, Math.Max(2, companyName.Length / 2)).ToLower()))
                                reasons.Add("Company Name");

                            blockedReason = string.Join(", ", reasons);
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(blockedReason))
                    {
                        blockedCustomers.Add(new ProspectCustomerBlocked
                        {
                            COMPANY_NAME = companyName,
                            CONTACT_PERSON = contactPerson,
                            CUSTOMER_CONTACT_NUMBER1 = customerNumber1,
                            CUSTOMER_CONTACT_NUMBER2 = customerNumber2,
                            CUSTOMER_CONTACT_NUMBER3 = customerNumber3,
                            CUSTOMER_EMAIL = customerEmail,
                            EMAIL_DOMAIN = emailDomain,
                            COUNTRY = country,
                            COUNTRY_CODE = countryCode,
                            STATE = state,
                            CITY = city,
                            CATEGORY = category,
                            CREATED_BY = username,
                            CREATED_ON = DateTime.UtcNow,
                            BLOCKED_BY = blockedByName,
                            BLOCK_REASON = blockedReason
                        });
                    }
                    else
                    {
                        cleanCustomers.Add(new ProspectCustomerClean
                        {
                            COMPANY_NAME = companyName,
                            CONTACT_PERSON = contactPerson,
                            CUSTOMER_CONTACT_NUMBER1 = customerNumber1,
                            CUSTOMER_CONTACT_NUMBER2 = customerNumber2,
                            CUSTOMER_CONTACT_NUMBER3 = customerNumber3,
                            CUSTOMER_EMAIL = customerEmail,
                            EMAIL_DOMAIN = emailDomain,
                            COUNTRY = country,
                            COUNTRY_CODE = countryCode,
                            STATE = state,
                            CITY = city,
                            CATEGORY = category,
                            CREATED_BY = username,
                            CREATED_ON = DateTime.UtcNow,
                            EVENT_NAME = "Default"
                        });
                    }
                }

                if (cleanCustomers.Any()) _context.CleanProspects.AddRange(cleanCustomers);
                if (blockedCustomers.Any()) _context.BlockedProspects.AddRange(blockedCustomers);
                await _context.SaveChangesAsync();

                var resultModel = new UploadResultViewModel
                {
                    BlockedCustomers = blockedCustomers,
                    CleanCustomers = cleanCustomers,
                    invalidCustomerRecords = invalidRecords
                };

                TempData["Message"] = "Upload successful.";
                TempData["MessageType"] = "Success";
                return View("UploadResults", resultModel);
            }
            catch (Exception)
            {
                TempData["Message"] = "Unexpected error occurred.";
                TempData["MessageType"] = "Error";
                return View("ViewRecords", new UploadResultViewModel());
            }
        }


        //[HttpPost]
        //public async Task<IActionResult> UploadSalesDataEvent(IFormFile file, string eventName, DateTime eventDate)
        //{
        //    try
        //    {
        //        var username = HttpContext.Session.GetString("Username");
        //        if (file != null && file.Length > 0)
        //        {
        //            var blockedCustomers = new List<ProspectCustomerBlocked>();
        //            var cleanCustomers = new List<ProspectCustomerClean>();
        //            var invalidRecords = new List<InvalidCustomerRecord>();

        //            using (var stream = new MemoryStream())
        //            {
        //                await file.CopyToAsync(stream);
        //                using (var workbook = new XLWorkbook(stream))
        //                {
        //                    var worksheet = workbook.Worksheet(1);
        //                    var lastRow = worksheet.LastRowUsed().RowNumber();

        //                    var emailSet = new HashSet<string>();
        //                    var duplicateEmails = new HashSet<string>();

        //                    for (int row = 2; row <= lastRow; row++) // Start from the third row (skip header)          
        //                    {
        //                        var companyName = worksheet.Cell(row, 1).GetString().Trim().ToUpper();
        //                        var contactPerson = worksheet.Cell(row, 2).GetString().Trim();
        //                        var customerNumber = worksheet.Cell(row, 3).GetString().Trim();
        //                        var customerNumber2 = worksheet.Cell(row, 7).GetString().Trim();
        //                        var customerNumber3 = worksheet.Cell(row, 8).GetString().Trim();
        //                        var customerEmail = worksheet.Cell(row, 4).GetString().Trim().Replace("\u00A0", "").ToLowerInvariant();
        //                        var countryCode = worksheet.Cell(row, 5).GetString()?.Trim();
        //                        var country = worksheet.Cell(row, 6).GetString().Trim();
        //                        var category = worksheet.Cell(row, 11).GetString().ToUpper().Trim();
        //                        var emailDomain = customerEmail?.Split('@').Last().ToLower();

        //                        var isCommonDomain = await _context.CommonDomains
        //                            .AnyAsync(d => d.DomainName.ToLower() == emailDomain);

        //                        bool isEmailEmpty = string.IsNullOrWhiteSpace(customerEmail);
        //                        bool isAllContactsEmpty = string.IsNullOrWhiteSpace(customerNumber) && string.IsNullOrWhiteSpace(customerNumber2) && string.IsNullOrWhiteSpace(customerNumber3);


        //                        if (isCommonDomain)
        //                        {
        //                            emailDomain = "NULL"; // Set to null if it is a common domain
        //                        }
        //                        if (!string.IsNullOrWhiteSpace(customerEmail))
        //                        {
        //                            if (emailSet.Contains(customerEmail))
        //                            {
        //                                duplicateEmails.Add(customerEmail); // Mark as duplicate
        //                            }
        //                            else
        //                            {
        //                                emailSet.Add(customerEmail); // Add to the set
        //                            }
        //                        }

        //                        if (!new[] { "CORPORATE", "LAWFIRM", "UNIVERSITY", "PCT", "MSME", "LAW FIRM", "INDIVIDUAL" }.Contains(category?.ToUpperInvariant()))
        //                        {
        //                            invalidRecords.Add(new InvalidCustomerRecord
        //                            {
        //                                RowNumber = row,
        //                                CompanyName = companyName,
        //                                CustomerEmail = customerEmail,
        //                                CustomerNumber = customerNumber,
        //                                ErrorMessage = "Invalid category."
        //                            });
        //                            continue;
        //                        }
        //                        if ((!IsValidPhoneNumber(customerNumber) || !IsValidPhoneNumber(customerNumber2) || !IsValidPhoneNumber(customerNumber3)))
        //                        {
        //                            invalidRecords.Add(new InvalidCustomerRecord
        //                            {
        //                                RowNumber = row,
        //                                CompanyName = companyName,
        //                                CustomerEmail = customerEmail,
        //                                CustomerNumber = $"{customerNumber}, {customerNumber2}, {customerNumber3}",
        //                                ErrorMessage = "Invalid Contact Number."
        //                            });
        //                            continue;
        //                        }
        //                        if (!IsValidEmail(customerEmail.Trim()) || duplicateEmails.Contains(customerEmail))
        //                        {
        //                            invalidRecords.Add(new InvalidCustomerRecord
        //                            {
        //                                RowNumber = row,
        //                                CompanyName = companyName,
        //                                CustomerEmail = customerEmail,
        //                                CustomerNumber = customerNumber,
        //                                ErrorMessage = duplicateEmails.Contains(customerEmail) ? "Duplicate email within the file." : "Invalid email format."
        //                            });
        //                            continue;
        //                        }
        //                        else if (string.IsNullOrWhiteSpace(companyName) || string.IsNullOrWhiteSpace(countryCode) || string.IsNullOrWhiteSpace(country))
        //                        {
        //                            invalidRecords.Add(new InvalidCustomerRecord
        //                            {
        //                                RowNumber = row,
        //                                CompanyName = companyName,
        //                                CustomerEmail = customerEmail,
        //                                CustomerNumber = customerNumber,
        //                                ErrorMessage = "Missing Mandatory Fields"
        //                            });
        //                            continue;
        //                        }
        //                        if (isEmailEmpty && isAllContactsEmpty)
        //                        {
        //                            invalidRecords.Add(new InvalidCustomerRecord
        //                            {
        //                                RowNumber = row,
        //                                CompanyName = companyName,
        //                                CustomerEmail = customerEmail,
        //                                CustomerNumber = $"{customerNumber}, {customerNumber2}, {customerNumber3}",
        //                                ErrorMessage = "Either Email or at least one Contact Number must be provided."
        //                            });
        //                            continue;
        //                        }
        //                        bool isAlreadyUploadedByOther = false;

        //                        var isAlreadyInMaster = await _context.Customers.Where(c => c.COMPANY_NAME.ToUpper() == companyName.ToUpper() || c.CUSTOMER_EMAIL.ToLower() == customerEmail.ToLower() || c.EMAIL_DOMAIN.ToLower() == emailDomain.ToLower()).AnyAsync();
        //                        if (emailDomain == "NULL")
        //                        {
        //                            isAlreadyUploadedByOther = await _context.CleanProspects.Where(c => ((c.COMPANY_NAME.ToUpper() == companyName.ToUpper() || c.CUSTOMER_EMAIL.ToLower() == customerEmail.ToLower()) && c.CREATED_BY != username)).AnyAsync();
        //                        }
        //                        else
        //                        {
        //                            isAlreadyUploadedByOther = await _context.CleanProspects.Where(c => ((c.COMPANY_NAME.ToUpper() == companyName.ToUpper() || c.EMAIL_DOMAIN.ToLower() == emailDomain.ToLower() || c.CUSTOMER_EMAIL.ToLower() == customerEmail.ToLower()) && c.CREATED_BY != username)).AnyAsync();
        //                        }

        //                        var isAlreadyUploadedBySameOrOther = await _context.Prospects.Where(c => c.CUSTOMER_EMAIL.ToLower() == customerEmail.ToLower()).AnyAsync();

        //                        // New logic: Check if record type is true in the Prospects table
        //                        var isBlockedInProspectTable = await _context.Prospects
        //                            .Where(c => c.RECORD_TYPE == true &&
        //                                        (c.COMPANY_NAME.ToUpper() == companyName.ToUpper() ||
        //                                         c.EMAIL_DOMAIN.ToLower() == emailDomain.ToLower()
        //                                         || c.CUSTOMER_EMAIL.ToLower() == customerEmail.ToLower()))
        //                            .AnyAsync();

        //                        var customerData = new ProspectCustomer
        //                        {
        //                            CUSTOMER_CODE = "1",
        //                            COMPANY_NAME = companyName,
        //                            CONTACT_PERSON = contactPerson,
        //                            CUSTOMER_CONTACT_NUMBER1 = customerNumber,
        //                            CUSTOMER_CONTACT_NUMBER2 = customerNumber2,
        //                            CUSTOMER_CONTACT_NUMBER3 = customerNumber3,
        //                            CUSTOMER_EMAIL = customerEmail,
        //                            COUNTRY = country,
        //                            STATE = worksheet.Cell(row, 10).GetString(),
        //                            CITY = worksheet.Cell(row, 11).GetString(),
        //                            CREATED_ON = eventDate,
        //                            CREATED_BY = username,
        //                            MODIFIED_BY = username,
        //                            MODIFIED_ON = DateTime.Now,
        //                            COUNTRY_CODE = countryCode,
        //                            EMAIL_DOMAIN = emailDomain,
        //                            CATEGORY = category,
        //                            EVENT_NAME = eventName,
        //                        };

        //                        // Apply blocking logic
        //                        if (isAlreadyUploadedByOther || isBlockedInProspectTable || isAlreadyUploadedBySameOrOther)
        //                        {
        //                            customerData.RECORD_TYPE = true; // Blocked
        //                            customerData.BLOCKED_BY = "Another User";
        //                            blockedCustomers.Add(customerData);
        //                        }
        //                        else
        //                        {
        //                            customerData.RECORD_TYPE = false; // Clean
        //                            cleanCustomers.Add(customerData);
        //                            _context.Prospects.Add(customerData);
        //                        }
        //                    }
        //                    await _context.SaveChangesAsync();
        //                }
        //            }

        //            var model = new UploadResultViewModel
        //            {
        //                BlockedCustomers = blockedCustomers,
        //                CleanCustomers = cleanCustomers,
        //                invalidCustomerRecords = invalidRecords
        //            };
        //            TempData["Message"] = "Successfully Uploaded";
        //            TempData["MessageType"] = "Success";
        //            return View("UploadResults", model);
        //        }

        //        return View();
        //    }
        //    catch (Exception ex)
        //    {
        //        var model = new UploadResultViewModel
        //        {

        //        };
        //        TempData["Message"] = "An unexpected error occurred. Please try again.";
        //        TempData["MessageType"] = "Error";
        //        return View("ViewRecords", model);
        //    }
        //}


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
            if (string.IsNullOrWhiteSpace(customerNumber)) return true;
            customerNumber = customerNumber.Trim();
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
                var blockedCustomers = JsonConvert.DeserializeObject<List<ProspectCustomerBlocked>>(BlockedCustomersJson);
                var cleanCustomers = JsonConvert.DeserializeObject<List<ProspectCustomerClean>>(CleanCustomersJson);
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
                    blockedSheet.Cell(1, 5).Value = "Blocked By";

                    // Fill data for blocked customers
                    for (int i = 0; i < blockedCustomers.Count; i++)
                    {
                        blockedSheet.Cell(i + 2, 1).Value = blockedCustomers[i].CUSTOMER_CODE;
                        blockedSheet.Cell(i + 2, 2).Value = blockedCustomers[i].COMPANY_NAME;
                        blockedSheet.Cell(i + 2, 3).Value = blockedCustomers[i].CUSTOMER_EMAIL;
                        blockedSheet.Cell(i + 2, 4).Value = blockedCustomers[i].CUSTOMER_CONTACT_NUMBER1;
                        blockedSheet.Cell(i + 2, 5).Value = blockedCustomers[i].BLOCKED_BY;
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
                var model = new UploadResultViewModel
                {

                };
                TempData["Message"] = "Too much data , Not able to Export";
                TempData["MessageType"] = "Error";
                return View("ViewRecords", model);
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

                    // Define headers with no asterisk
                    worksheet.Cell(1, 1).Value = "Company Name";
                    worksheet.Cell(1, 2).Value = "Contact Person";
                    worksheet.Cell(1, 3).Value = "Contact No1";
                    worksheet.Cell(1, 4).Value = "Email";
                    worksheet.Cell(1, 5).Value = "Country Code";
                    worksheet.Cell(1, 6).Value = "Country";
                    worksheet.Cell(1, 7).Value = "Contact No2";
                    worksheet.Cell(1, 8).Value = "Contact No3";
                    worksheet.Cell(1, 9).Value = "State";
                    worksheet.Cell(1, 10).Value = "City";
                    worksheet.Cell(1, 11).Value = "Category";
                    worksheet.Cell(1, 12).Value = "Example"; // Help column

                    // Style headers (Red for required, Blue/Black for optional)
                    var headerRange = worksheet.Range("A1:L1");
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    headerRange.Style.Border.OutsideBorderColor = XLColor.Black;
                    headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                    headerRange.Style.Border.InsideBorderColor = XLColor.Black;

                    // Applying background color and italic styling for headers
                    worksheet.Cell(1, 1).Style.Font.FontColor = XLColor.Red;
                    worksheet.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.LightYellow;
                    worksheet.Cell(1, 1).Style.Font.Italic = true;

                    worksheet.Cell(1, 2).Style.Font.FontColor = XLColor.Red;
                    worksheet.Cell(1, 2).Style.Fill.BackgroundColor = XLColor.LightYellow;
                    worksheet.Cell(1, 2).Style.Font.Italic = true;

                    worksheet.Cell(1, 4).Style.Font.FontColor = XLColor.Red;
                    worksheet.Cell(1, 4).Style.Fill.BackgroundColor = XLColor.LightYellow;
                    worksheet.Cell(1, 4).Style.Font.Italic = true;

                    worksheet.Cell(1, 5).Style.Font.FontColor = XLColor.Red;
                    worksheet.Cell(1, 5).Style.Fill.BackgroundColor = XLColor.LightYellow;
                    worksheet.Cell(1, 5).Style.Font.Italic = true;

                    worksheet.Cell(1, 6).Style.Font.FontColor = XLColor.Red;
                    worksheet.Cell(1, 6).Style.Fill.BackgroundColor = XLColor.LightYellow;
                    worksheet.Cell(1, 6).Style.Font.Italic = true;

                    worksheet.Cell(1, 11).Style.Font.FontColor = XLColor.Red;
                    worksheet.Cell(1, 11).Style.Fill.BackgroundColor = XLColor.LightYellow;
                    worksheet.Cell(1, 11).Style.Font.Italic = true;

                    // Optional fields in Blue/Black with background color
                    worksheet.Cell(1, 3).Style.Font.FontColor = XLColor.Blue;
                    worksheet.Cell(1, 3).Style.Fill.BackgroundColor = XLColor.LightCyan;
                    worksheet.Cell(1, 3).Style.Font.Italic = true;

                    worksheet.Cell(1, 7).Style.Font.FontColor = XLColor.Blue;
                    worksheet.Cell(1, 7).Style.Fill.BackgroundColor = XLColor.LightCyan;
                    worksheet.Cell(1, 7).Style.Font.Italic = true;

                    worksheet.Cell(1, 8).Style.Font.FontColor = XLColor.Blue;
                    worksheet.Cell(1, 8).Style.Fill.BackgroundColor = XLColor.LightCyan;
                    worksheet.Cell(1, 8).Style.Font.Italic = true;

                    worksheet.Cell(1, 9).Style.Font.FontColor = XLColor.Blue;
                    worksheet.Cell(1, 9).Style.Fill.BackgroundColor = XLColor.LightCyan;
                    worksheet.Cell(1, 9).Style.Font.Italic = true;

                    worksheet.Cell(1, 10).Style.Font.FontColor = XLColor.Blue;
                    worksheet.Cell(1, 10).Style.Fill.BackgroundColor = XLColor.LightCyan;
                    worksheet.Cell(1, 10).Style.Font.Italic = true;

                    worksheet.Cell(1, 12).Style.Font.FontColor = XLColor.Gray;
                    worksheet.Cell(1, 12).Style.Fill.BackgroundColor = XLColor.LightGray;
                    worksheet.Cell(1, 12).Style.Font.Italic = true;

                    // Example row (Row 2) with gray italic text and background color
                    worksheet.Cell(2, 1).Value = "Ennoble Ip";
                    worksheet.Cell(2, 2).Value = "Rajnish Sir";
                    worksheet.Cell(2, 3).Value = "123456789";
                    worksheet.Cell(2, 4).Value = "ennobleip@gmail.com";
                    worksheet.Cell(2, 5).Value = "+91";
                    worksheet.Cell(2, 6).Value = "INDIA";
                    worksheet.Cell(2, 7).Value = "9876543210";
                    worksheet.Cell(2, 8).Value = "9876543210";
                    worksheet.Cell(2, 9).Value = "DELHI";
                    worksheet.Cell(2, 10).Value = "NEW DELHI";
                    worksheet.Cell(2, 11).Value = "Corporate/Law Firm/MSME/University/PCT/Individual";
                    worksheet.Cell(2, 12).Value = "Please delete This row and follow this format.";

                    // Style the example row (Gray, italic and background color)
                    var exampleRow = worksheet.Range("A2:L2");
                    exampleRow.Style.Font.FontColor = XLColor.Black;
                    exampleRow.Style.Font.Italic = true;

                    // Set custom column widths
                    worksheet.Column(1).Width = 20;
                    worksheet.Column(2).Width = 20;
                    worksheet.Column(3).Width = 15;
                    worksheet.Column(4).Width = 25;
                    worksheet.Column(5).Width = 12;
                    worksheet.Column(6).Width = 15;
                    worksheet.Column(7).Width = 15;
                    worksheet.Column(8).Width = 15;
                    worksheet.Column(9).Width = 15;
                    worksheet.Column(10).Width = 18;
                    worksheet.Column(11).Width = 50;
                    worksheet.Column(12).Width = 50; // Example column

                    // Add a note to help the user (in the Example column)
                    worksheet.Cell(3, 12).Value = "Red headers are mandatory. Either 'Email' or 'Contact No' is required.";

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
                var model = new UploadResultViewModel();
                TempData["Message"] = "An unexpected error occurred. Please try again.";
                TempData["MessageType"] = "Error";
                return View("ViewRecords", model);
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

                    // Set headers
                    worksheet.Cell(1, 1).Value = "*CompanyName";
                    worksheet.Cell(1, 2).Value = "*ContactPerson";
                    worksheet.Cell(1, 3).Value = "ContactNo1";
                    worksheet.Cell(1, 4).Value = "*Email";
                    worksheet.Cell(1, 5).Value = "*CountryCode";
                    worksheet.Cell(1, 6).Value = "*Country";
                    worksheet.Cell(1, 7).Value = "ContactNo2";
                    worksheet.Cell(1, 8).Value = "ContactNo3";
                    worksheet.Cell(1, 9).Value = "State";
                    worksheet.Cell(1, 10).Value = "City";
                    worksheet.Cell(1, 11).Value = "*Category";

                    // Sample row data
                    worksheet.Cell(2, 1).Value = "Ennoble Ip";
                    worksheet.Cell(2, 2).Value = "Rajnish Sir";
                    worksheet.Cell(2, 3).Value = "123456789";
                    worksheet.Cell(2, 4).Value = "ennobleip@gmail.com";
                    worksheet.Cell(2, 5).Value = "+91";
                    worksheet.Cell(2, 6).Value = "INDIA";
                    worksheet.Cell(2, 7).Value = "9876543210";
                    worksheet.Cell(2, 8).Value = "9876543210";
                    worksheet.Cell(2, 9).Value = "DELHI";
                    worksheet.Cell(2, 10).Value = "NEW DELHI";
                    worksheet.Cell(2, 11).Value = "Corporate/Law Firm/MSME/University/PCT/Individual";

                    // Adjust column widths to fit content
                    worksheet.Columns().AdjustToContents();

                    // Apply styles to header row
                    var headerRow = worksheet.Range("A1:K1");
                    headerRow.Style.Font.Bold = true;
                    headerRow.Style.Font.FontColor = XLColor.White;
                    headerRow.Style.Fill.BackgroundColor = XLColor.DarkBlue;
                    headerRow.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    headerRow.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    headerRow.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
                    headerRow.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                    // Apply styles to data row
                    var dataRow = worksheet.Range("A2:K2");
                    dataRow.Style.Font.FontColor = XLColor.Black;
                    dataRow.Style.Fill.BackgroundColor = XLColor.LightYellow;
                    dataRow.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    dataRow.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    dataRow.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    dataRow.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                    // Freeze top row
                    worksheet.SheetView.FreezeRows(1);

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
                var model = new UploadResultViewModel { };
                TempData["Message"] = "An unexpected error occurred. Please try again.";
                TempData["MessageType"] = "Error";
                return View("ViewRecords", model);
            }
        }



        [HttpPost]
        public async Task<IActionResult> ViewRecord(UploadResultViewModel model)
        {
            try
            {
                var username = HttpContext.Session.GetString("Username");
                var category = model.Category?.ToUpper()?.Trim();
                var eventName = model.Event?.Trim();
                var selectedDate = model.SelectedDate?.Date;

                // Clear any previous data
                model.CleanCustomers = new List<ProspectCustomerClean>();
                model.BlockedCustomers = new List<ProspectCustomerBlocked>();

                // Fetch based on Record Type
                if (model.RecordType == "Blocked")
                {
                    model.BlockedCustomers = await _context.BlockedProspects
                        .Where(c => c.CREATED_BY == username &&
                            (string.IsNullOrEmpty(category) || c.CATEGORY.ToUpper() == category) &&
                            (string.IsNullOrEmpty(eventName) || c.EVENT_NAME == eventName) &&
                            (!selectedDate.HasValue || c.CREATED_ON.HasValue && c.CREATED_ON.Value.Date == selectedDate))
                        .ToListAsync();
                }
                else if (model.RecordType == "Clean")
                {
                    model.CleanCustomers = await _context.CleanProspects
                        .Where(c =>
                            c.CREATED_BY == username &&
                            (string.IsNullOrEmpty(category) || c.CATEGORY.ToUpper() == category) &&
                            (string.IsNullOrEmpty(eventName) || c.EVENT_NAME == eventName) &&
                            (!selectedDate.HasValue || c.CREATED_ON.HasValue && c.CREATED_ON.Value.Date == selectedDate))
                        .ToListAsync();
                }
                else // "All" or blank
                {
                    model.CleanCustomers = await _context.CleanProspects
                        .Where(c =>
                            c.CREATED_BY == username &&
                            (string.IsNullOrEmpty(category) || c.CATEGORY.ToUpper() == category) &&
                            (string.IsNullOrEmpty(eventName) || c.EVENT_NAME == eventName) &&
                            (!selectedDate.HasValue || c.CREATED_ON.HasValue && c.CREATED_ON.Value.Date == selectedDate))
                        .ToListAsync();

                    model.BlockedCustomers = await _context.BlockedProspects
                        .Where(c =>
                            c.BLOCKED_BY == username &&
                            (string.IsNullOrEmpty(category) || c.CATEGORY.ToUpper() == category) &&
                            (string.IsNullOrEmpty(eventName) || c.EVENT_NAME == eventName) &&
                            (!selectedDate.HasValue || c.CREATED_ON.HasValue && c.CREATED_ON.Value.Date == selectedDate))
                        .ToListAsync();
                }

                // Set message
                if (model.CleanCustomers.Any() || model.BlockedCustomers.Any())
                {
                    TempData["Message"] = "Successfully record(s) found.";
                    TempData["MessageType"] = "Success";
                }
                else
                {
                    TempData["Message"] = "No record(s) found.";
                    TempData["MessageType"] = "Info";
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
