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


        [HttpPost]
        public async Task<IActionResult> UploadSalesData(IFormFile file)
        {
            if (file != null && file.Length > 0)
            {
                var blockedCustomers = new List<ProspectCustomer>();
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
                            var email = worksheet.Cell(row, 7).GetString();

                            // Check if the customer exists
                            var existingCustomer = await _context.Customers.FirstOrDefaultAsync(c => c.CUSTOMER_EMAIL.ToLower() == email.Trim().ToLower());
                            var prospectCustomer = await _context.Prospects.FirstOrDefaultAsync(c => c.CUSTOMER_EMAIL.ToLower() == email.Trim().ToLower());

                            var customerData = new ProspectCustomer
                            {
                                CUSTOMER_CODE = worksheet.Cell(row, 1).GetString(),
                                CUSTOMER_NAME = worksheet.Cell(row, 2).GetString(),
                                CONTACT_PERSON = worksheet.Cell(row, 3).GetString(),
                                CUSTOMER_CONTACT_NUMBER1 = worksheet.Cell(row, 4).GetString(),
                                CUSTOMER_CONTACT_NUMBER2 = worksheet.Cell(row, 5).GetString(),
                                CUSTOMER_CONTACT_NUMBER3 = worksheet.Cell(row, 6).GetString(),
                                CUSTOMER_EMAIL = worksheet.Cell(row, 7).GetString(),
                                COUNTRY = worksheet.Cell(row, 8).GetString(),
                                STATE = worksheet.Cell(row, 9).GetString(),
                                CITY = worksheet.Cell(row, 10).GetString(),
                                CREATED_ON = DateTime.Now,
                                CREATED_BY = "Admin",
                                MODIFIED_BY = "Admin",
                                MODIFIED_ON = DateTime.Now
                            };

                            // If existing customer is found, mark as blocked (RECORD_TYPE = true)
                            if (existingCustomer != null || prospectCustomer != null)
                            {
                                customerData.RECORD_TYPE = true; // Blocked
                                customerData.IS_EMAIL_BLOCKED = true;
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
                    cleanSheet.Cell(i + 2, 2).Value = cleanCustomers[i].CUSTOMER_NAME;
                    cleanSheet.Cell(i + 2, 3).Value = cleanCustomers[i].CUSTOMER_EMAIL;
                    cleanSheet.Cell(i + 2, 4).Value = cleanCustomers[i].CUSTOMER_CONTACT_NUMBER1;
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


        [HttpPost]
        [HttpPost]
        public async Task<IActionResult> ViewRecord(UploadResultViewModel model)
        {
            if (model.RecordType == null)
            {
                return View("ViewRecords", model);
            }

            var filteredCustomers = new List<ProspectCustomer>();

            if (model.RecordType == "Blocked")
            {
                // Fetch blocked customers (RECORD_TYPE = 1) based on the selected date
                filteredCustomers = await _context.Prospects
                    .Where(c => c.RECORD_TYPE == true &&
                                (model.SelectedDate == null || c.CREATED_ON.HasValue && c.CREATED_ON.Value.Date == model.SelectedDate.Value.Date))
                    .ToListAsync();
                model.BlockedCustomers = filteredCustomers;
            }
            else if (model.RecordType == "Clean")
            {
                // Fetch clean customers (RECORD_TYPE = 0) based on the selected date
                filteredCustomers = await _context.Prospects
                    .Where(c => c.RECORD_TYPE == false &&
                                (model.SelectedDate == null || c.CREATED_ON.HasValue && c.CREATED_ON.Value.Date == model.SelectedDate.Value.Date))
                    .ToListAsync();
                model.CleanCustomers = filteredCustomers;
            }
            else
            {
                // Fetch both blocked and clean customers based on the selected date
                filteredCustomers = await _context.Prospects
                    .Where(c => model.SelectedDate == null || c.CREATED_ON.HasValue && c.CREATED_ON.Value.Date == model.SelectedDate.Value.Date)
                    .ToListAsync();
                //model.BlockedCustomers = filteredCustomers;
            }

            // Populate the view model with the filtered data
            

            return View("ViewRecords", model);
        }



        [HttpPost]
        public async Task<IActionResult> UpdateCustomerStatus(List<int> BlockedCustomerIds, List<int> CleanCustomerIds)
        {
            // Change blocked customers to clean
            if (BlockedCustomerIds != null && BlockedCustomerIds.Any())
            {
                var blockedCustomers = await _context.Prospects
                    .Where(c => BlockedCustomerIds.Contains(c.ID))
                    .ToListAsync();

                foreach (var customer in blockedCustomers)
                {
                    customer.IS_EMAIL_BLOCKED = false; // Change to clean
                }

                await _context.SaveChangesAsync();
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
                }

                await _context.SaveChangesAsync();
            }

            // Redirect back to the ViewEmailRecords action with the selected RecordType and SelectedDate
            return RedirectToAction("ViewEmailRecords", new { RecordType = "Blocked", SelectedDate = DateTime.Now }); // Adjust as needed
        }

        [HttpPost]
        public async Task<IActionResult> ViewEmailRecords(string RecordType, DateTime? SelectedDate)
        {
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
                    .Where(c => c.RECORD_TYPE == false && c.IS_EMAIL_BLOCKED == true &&
                                (!SelectedDate.HasValue || c.CREATED_ON.Value.Date == SelectedDate.Value.Date))
                    .ToListAsync();
            }
            // Clean records: RecordType == 0 and IS_EMAIL_BLOCKED == false
            else if (isClean)
            {
                model.CleanCustomersEmailList = await _context.Prospects
                    .Where(c => c.RECORD_TYPE == false && c.IS_EMAIL_BLOCKED == false &&
                                (!SelectedDate.HasValue || c.CREATED_ON.Value.Date == SelectedDate.Value.Date))
                    .ToListAsync();
            }
            // If no specific record type is selected, show both Blocked and Clean records for the given date
            else
            {
                model.BlockCustomersEmailList = await _context.Prospects
                    .Where(c => c.RECORD_TYPE == false && c.IS_EMAIL_BLOCKED == true &&
                                (!SelectedDate.HasValue || c.CREATED_ON.Value.Date == SelectedDate.Value.Date))
                    .ToListAsync();

                model.CleanCustomersEmailList = await _context.Prospects
                    .Where(c => c.RECORD_TYPE == false && c.IS_EMAIL_BLOCKED == false &&
                                (!SelectedDate.HasValue || c.CREATED_ON.Value.Date == SelectedDate.Value.Date))
                    .ToListAsync();
            }

            return View("ViewRecords", model); // Return the view with the populated UploadResultViewModel
        }












    }
}
