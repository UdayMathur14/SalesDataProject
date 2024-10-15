using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SalesDataProject.Models;

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
            return View();
        }
        public IActionResult UploadResults(UploadResultViewModel model)
        {
            return View(model);

        }
        public IActionResult ViewRecords(UploadResultViewModel model)
        {
            return View(model);

        }
        public async Task<IActionResult> BlockedEmail(ProspectCustomer model)
        {
            var prospectCustomers = await _context.Prospects.Where(c => !c.IsEmailBlocked).ToListAsync();
            return View(prospectCustomers);

        }


        [HttpPost]
        public async Task<IActionResult> UploadSalesData(IFormFile file)
        {
            if (file != null && file.Length > 0)
            {
                var blockedCustomers = new List<Customer>();
                var cleanCustomers = new List<ProspectCustomer>();

                using (var stream = new MemoryStream())
                {
                    await file.CopyToAsync(stream);
                    using (var workbook = new XLWorkbook(stream))
                    {
                        var worksheet = workbook.Worksheet(1);
                        var lastRow = worksheet.LastRowUsed().RowNumber();

                        for (int row = 2; row <= lastRow; row++) // Start from the second row (skip header)
                        {
                            var country = worksheet.Cell(row, 3).GetString();

                            // Check if the customer exists
                            var existingCustomer = await _context.Customers.FirstOrDefaultAsync(c => c.CUSTOMER_EMAIL.ToLower() == country.Trim().ToLower());
                            var prospectCustomer = await _context.Prospects.FirstOrDefaultAsync(c => c.CUSTOMER_EMAIL.ToLower() == country.Trim().ToLower());

                            
                            if (existingCustomer != null)
                            {
                                blockedCustomers.Add(existingCustomer);
                                var blockedCustomer = new BlockedCustomer
                                {
                                    CUSTOMER_CODE = worksheet.Cell(row, 1).GetString(),
                                    CUSTOMER_NAME = worksheet.Cell(row, 2).GetString(),
                                    CUSTOMER_EMAIL = worksheet.Cell(row, 3).GetString(),
                                    CUSTOMER_CONTACT_NUMBER = worksheet.Cell(row, 4).GetString(),
                                    COUNTRY = worksheet.Cell(row, 5).GetString(),
                                    CITY = worksheet.Cell(row, 6).GetString(),
                                    STATE = worksheet.Cell(row, 7).GetString(),
                                    CREATED_ON = DateTime.Now,
                                    BlockedDate = DateTime.Now,
                                    CREATED_BY = "Admin" , 
                                    MODIFIED_BY = "Admin",
                                    MODIFIED_ON = DateTime.Now
                                };
                                _context.BlockedCustomers.Add(blockedCustomer);
                            }
                            else if (prospectCustomer != null)
                            {
                                var blockedCust = new BlockedCustomer
                                {
                                    CUSTOMER_CODE = worksheet.Cell(row, 1).GetString(),
                                    CUSTOMER_NAME = worksheet.Cell(row, 2).GetString(),
                                    CUSTOMER_EMAIL = worksheet.Cell(row, 3).GetString(),
                                    CUSTOMER_CONTACT_NUMBER = worksheet.Cell(row, 4).GetString(),
                                    COUNTRY = worksheet.Cell(row, 5).GetString(),
                                    CITY = worksheet.Cell(row, 6).GetString(),
                                    STATE = worksheet.Cell(row, 7).GetString(),
                                    CREATED_ON = DateTime.Now,
                                    BlockedDate = DateTime.Now,
                                    CREATED_BY = "Admin",
                                    MODIFIED_BY = "Admin",
                                    MODIFIED_ON = DateTime.Now
                                };
                                _context.BlockedCustomers.Add(blockedCust);

                                var blockedCustomerDetails = new Customer
                                {
                                    CUSTOMER_CODE = worksheet.Cell(row, 1).GetString(),
                                    CUSTOMER_NAME = worksheet.Cell(row, 2).GetString(),
                                    CUSTOMER_EMAIL = worksheet.Cell(row, 3).GetString(),
                                    CUSTOMER_CONTACT_NUMBER = worksheet.Cell(row, 4).GetString(),
                                    COUNTRY = worksheet.Cell(row, 5).GetString(),
                                    CITY = worksheet.Cell(row, 6).GetString(),
                                    STATE = worksheet.Cell(row, 7).GetString(),
                                    CREATED_ON = DateTime.Now,
                                    CREATED_BY = "Admin",
                                    MODIFIED_BY = "Admin",
                                    MODIFIED_ON = DateTime.Now
                                };

                                blockedCustomers.Add(blockedCustomerDetails);

                            }
                            else
                            {
                                var newCustomer = new ProspectCustomer
                                {
                                    CUSTOMER_CODE = worksheet.Cell(row, 1).GetString(),
                                    CUSTOMER_NAME = worksheet.Cell(row, 2).GetString(),
                                    CUSTOMER_EMAIL = worksheet.Cell(row, 3).GetString(),
                                    CUSTOMER_CONTACT_NUMBER = worksheet.Cell(row, 4).GetString(),
                                    COUNTRY = worksheet.Cell(row, 5).GetString(),
                                    CITY = worksheet.Cell(row, 6).GetString(),
                                    STATE = worksheet.Cell(row, 7).GetString(),
                                    CREATED_ON = DateTime.Now,
                                    CREATED_BY = "Admin",
                                    MODIFIED_BY = "Admin",
                                    MODIFIED_ON = DateTime.Now
                                };
                                cleanCustomers.Add(newCustomer);
                                _context.Prospects.Add(newCustomer);
                            }
                        }

                        await _context.SaveChangesAsync();
                    }
                }

                var model = new UploadResultViewModel
                {
                    BlockedCustomers = blockedCustomers,
                    CleanCustomers = cleanCustomers
                };

                // Return view with blocked and clean customers
                return View("UploadResults", model);
            }

            return View();
        }
        [HttpPost]
        public IActionResult ExportToExcel(string BlockedCustomersJson, string CleanCustomersJson)
        {
            var blockedCustomers = JsonConvert.DeserializeObject<List<Customer>>(BlockedCustomersJson);
            var cleanCustomers = JsonConvert.DeserializeObject<List<Customer>>(CleanCustomersJson);

            using (var workbook = new XLWorkbook())
            {
                var blockedSheet = workbook.Worksheets.Add("Blocked Customers");
                var cleanSheet = workbook.Worksheets.Add("Clean Customers");

                // Add headers for blocked customers
                blockedSheet.Cell(1, 1).Value = "Customer Code";
                blockedSheet.Cell(1, 2).Value = "Customer Name";
                blockedSheet.Cell(1, 3).Value = "Email";
                blockedSheet.Cell(1, 4).Value = "Contact Number";

                // Fill data for blocked customers
                for (int i = 0; i < blockedCustomers.Count; i++)
                {
                    blockedSheet.Cell(i + 2, 1).Value = blockedCustomers[i].CUSTOMER_CODE;
                    blockedSheet.Cell(i + 2, 2).Value = blockedCustomers[i].CUSTOMER_NAME;
                    blockedSheet.Cell(i + 2, 3).Value = blockedCustomers[i].CUSTOMER_EMAIL;
                    blockedSheet.Cell(i + 2, 4).Value = blockedCustomers[i].CUSTOMER_CONTACT_NUMBER;
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
                    cleanSheet.Cell(i + 2, 2).Value = cleanCustomers[i].CUSTOMER_NAME;
                    cleanSheet.Cell(i + 2, 3).Value = cleanCustomers[i].CUSTOMER_EMAIL;
                    cleanSheet.Cell(i + 2, 4).Value = cleanCustomers[i].CUSTOMER_CONTACT_NUMBER;
                }

                // Prepare the memory stream to send the Excel file
                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "CustomerUploadResults.xlsx");
                }
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
        public async Task<IActionResult> ViewRecord(UploadResultViewModel model)
        {
            var filteredBlockedCustomers = new List<BlockedCustomer>();
            var filteredProspectCustomers = new List<ProspectCustomer>();

            if (model.RecordType == "Blocked")
            {
                // Fetch blocked customers based on the selected date
                filteredBlockedCustomers = await _context.BlockedCustomers
                    .Where(c => model.SelectedDate == null || c.BlockedDate.Date == model.SelectedDate.Value.Date)
                    .ToListAsync();
            }
            else if (model.RecordType == "Clean")
            {
                // Fetch prospect (clean) customers based on the selected date
                filteredProspectCustomers = await _context.Prospects
                    .Where(c => model.SelectedDate == null || c.CREATED_ON.Date == model.SelectedDate.Value.Date)
                    .ToListAsync();
            }

            // Populate the view model with the filtered data
            model.blockCustomer = filteredBlockedCustomers;
            model.CleanCustomers = filteredProspectCustomers;

            return View("ViewRecords", model);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateBlockedEmails(int[] selectedCustomers)
        {
            if (selectedCustomers != null && selectedCustomers.Length > 0)
            {
                var customersToUpdate = await _context.Prospects
                    .Where(c => selectedCustomers.Contains(c.ID))
                    .ToListAsync();

                foreach (var customer in customersToUpdate)
                {
                    customer.IsEmailBlocked = true; // Mark as blocked
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Selected emails have been marked as blocked.";
            }
            else
            {
                TempData["ErrorMessage"] = "No records selected.";
            }

            return RedirectToAction("BlockedEmail");
        }

    }
}
