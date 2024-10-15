using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesDataProject.Models;

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
                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Customer has been successfully created.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> UploadExcel(IFormFile file)
        {
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

                        for (int row = 2; row <= lastRow; row++) // Start from the second row (skip header)
                        {
                            var customer = new Customer
                            {
                                CUSTOMER_CODE = worksheet.Cell(row, 1).GetString(),
                                CUSTOMER_NAME = worksheet.Cell(row, 2).GetString(),
                                CUSTOMER_EMAIL = worksheet.Cell(row, 3).GetString(),
                                CUSTOMER_CONTACT_NUMBER = worksheet.Cell(row, 4).GetString(),
                                COUNTRY = worksheet.Cell(row, 5).GetString(),
                                CITY = worksheet.Cell(row, 6).GetString(),
                                STATE = worksheet.Cell(row, 7).GetString(),
                                CREATED_BY = "Admin", // Set this based on your logic
                                CREATED_ON = DateTime.Now,
                                MODIFIED_BY = "Admin", // Set this based on your logic
                                MODIFIED_ON = DateTime.Now
                            };
                            _context.Customers.Add(customer);
                        }
                        await _context.SaveChangesAsync();
                    }
                }
                TempData["SuccessMessage"] = "Succefully Uploaded";
                return RedirectToAction(nameof(Index));
            }
            return View(); // Handle error or return to the form if upload fails
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

    }
}
