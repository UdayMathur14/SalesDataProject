using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using SalesDataProject.Models;
using System.Text.RegularExpressions;
namespace SalesDataProject.Controllers
{
    public class TitleController : Controller
    {
        private readonly AppDbContext _context;

        public TitleController(AppDbContext context)
        {
            _context = context;
        }
        public IActionResult Index(ValidationResultViewModel model)
        {
            return View(model);
        }
        public async Task<IActionResult> ViewTitles()
        {
            var titles = await _context.Titles.ToListAsync();
            return View(titles);
        }

        [HttpPost]
        public async Task<IActionResult> UploadExcel(IFormFile file)
        {
            var username = HttpContext.Session.GetString("Username");
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded.");
            }

            var result = new ValidationResultViewModel(); // Initialize result
            bool hasInvalidInvoiceNumber = false; // Flag to check for invalid rows

            using (var package = new ExcelPackage(file.OpenReadStream()))
            {
                var worksheet = package.Workbook.Worksheets[0];
                var rowCount = worksheet.Dimension.Rows;

                // Fetch all Titles from the database
                var allTitles = await _context.Titles.ToListAsync();

                // Loop through each row in the worksheet (starting from row 2 to skip the header)
                for (int row = 2; row <= rowCount; row++)
                {
                    var invoiceNumber = worksheet.Cells[row, 1].Text;
                    var codeReference = worksheet.Cells[row, 2].Text;
                    var cleantitle = worksheet.Cells[row, 3].Text;

                    // Clean and concatenate the title
                    string concatenatedTitle = CleanTitle(cleantitle);

                    // Check if the InvoiceNumber is empty or null
                    if (string.IsNullOrWhiteSpace(invoiceNumber))
                    {
                        hasInvalidInvoiceNumber = true; // Set the flag if an invalid row is found
                    }

                    // Check if the concatenated title matches any ReferenceTitle in the database
                    var existingTitle = allTitles
                        .FirstOrDefault(t => (t.ReferenceTitle) == concatenatedTitle);

                    // Create a TitleValidationViewModel for the current row
                    var titleValidation = new TitleValidationViewModel
                    {
                        RowNumber = row,
                        Title = cleantitle, // Save the concatenated title
                        InvoiceNumber = invoiceNumber,
                        CodeReference = codeReference,
                        CREATED_ON = DateOnly.FromDateTime(DateTime.Now),
                        CREATED_BY = username,
                        Status = existingTitle != null ? "Blocked" : "Clean",
                        ReferenceTitle = concatenatedTitle,
                        BlockedId = existingTitle?.Id
                    };

                    // Add the titleValidation object to the appropriate list
                    if (existingTitle != null)
                    {
                        result.BlockedTitles.Add(titleValidation); // Add to BlockedTitles if it already exists
                    }
                    else
                    {
                        result.CleanTitles.Add(titleValidation); // Add to CleanTitles if it doesn't exist
                    }
                }
            }

            // Save only clean records to the database if all rows have valid InvoiceNumbers
            if (!hasInvalidInvoiceNumber)
            {
                // Filter only clean records for saving
                var cleanRecordsToSave = result.CleanTitles
                    .Select(tv => new TitleValidationViewModel
                    {
                        RowNumber = tv.RowNumber,
                        Title = tv.Title,
                        InvoiceNumber = tv.InvoiceNumber,
                        CodeReference = tv.CodeReference,
                        CREATED_ON = tv.CREATED_ON,
                        CREATED_BY = tv.CREATED_BY,
                        Status = tv.Status,
                        ReferenceTitle = CleanTitle(tv.Title) // Save the concatenated title as ReferenceTitle
                    }).ToList();

                if (cleanRecordsToSave.Any())
                {
                    _context.Titles.AddRange(cleanRecordsToSave); // Add to the database context
                    await _context.SaveChangesAsync(); // Save changes
                    TempData["messagesuccess"] = "Successfully Saved";
                }
            }
            else
            {
                TempData["Error"] = "One or more rows have an empty Invoice Number. No data has been saved to the database.";
            }

            // Return the result to the view
            return View("Index", result);
        }



        [HttpGet]
        public IActionResult DownloadTemplate()
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Titles");

                // Define the headers in the template
                worksheet.Cell(1, 1).Value = "InvoiceNumber";
                worksheet.Cell(1, 2).Value = "CodeReference";
                worksheet.Cell(1, 3).Value = "Title";

                // Set the column width specifically for the "Title" column
                worksheet.Column(3).Width = 11.0; // Approximate width for 3 cm

                // Optionally, adjust the other columns to fit content
                worksheet.Column(1).AdjustToContents();
                worksheet.Column(2).AdjustToContents();

                // Optionally, apply styles to the header row for better visibility
                var headerRow = worksheet.Range("A1:C1");
                headerRow.Style.Font.Bold = true;
                headerRow.Style.Font.FontColor = XLColor.Red;

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "UploadTitles.xlsx");
                }
            }
        }


        private string CleanTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return string.Empty;

            // Remove all special characters and spaces
            string cleanedTitle = Regex.Replace(title, @"[^a-zA-Z0-9]", "");

            return cleanedTitle.ToLower(); // Convert to lowercase for uniformity
        }

        //[HttpPost]
        //public IActionResult InsertCleanTitles(string cleanTitles)
        //{
        //    // Check if the hidden input value is null or empty
        //    if (!string.IsNullOrEmpty(cleanTitles))
        //    {
        //        // Deserialize the JSON array into a list of strings
        //        var titlesList = JsonSerializer.Deserialize<List<string>>(cleanTitles);

        //        if (titlesList != null && titlesList.Any())
        //        {
        //            // Iterate through each title and save it to the database
        //            foreach (var title in titlesList)
        //            {
        //                var newTitle = new TitleValidationViewModel
        //                {
        //                    RowNumber = 1, // Example row number; adjust as needed
        //                    Title = title, // Add the title
        //                    Status = "Clean",  // Add the current timestamp
        //                    CREATED_ON = DateOnly.FromDateTime(DateTime.Now)
        //                };

        //                // Add the title to the database context
        //                _context.Titles.Add(newTitle);
        //            }

        //            // Save changes to the database
        //            _context.SaveChanges();
        //        }
        //    }
        //    TempData["messagesuccess"] = "Successfully Uploaded";
        //    // Redirect to the Index page after insertion
        //    return RedirectToAction("Index");
        //}


        [HttpPost]
        public async Task<IActionResult> DeleteSelected(List<int> selectedIds)
        {
            if (selectedIds == null || !selectedIds.Any())
            {
                TempData["Error"] = "No records selected for deletion.";
                return RedirectToAction("ViewTitles");
            }

            var titlesToDelete = _context.Titles.Where(t => selectedIds.Contains(t.Id)).ToList();

            if (titlesToDelete.Any())
            {
                _context.Titles.RemoveRange(titlesToDelete);
                await _context.SaveChangesAsync();
                TempData["messagesuccess"] = "Selected records deleted successfully.";
            }
            else
            {
                TempData["Error"] = "No matching records found for deletion.";
            }

            return RedirectToAction("ViewTitles");
        }




    }
}
