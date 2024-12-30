using ClosedXML.Excel;
using DocumentFormat.OpenXml.Bibliography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using SalesDataProject.Models;
using System.Text.Json;
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

        [HttpPost]
        public async Task<IActionResult> UploadExcel(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded.");
            }

            var result = new ValidationResultViewModel(); // Ensure result is initialized

            using (var package = new ExcelPackage(file.OpenReadStream()))
            {
                var worksheet = package.Workbook.Worksheets[0];
                var rowCount = worksheet.Dimension.Rows;

                // Fetch all Titles from the database
                var allTitles = await _context.Titles.ToListAsync();

                // Loop through each row in the worksheet (starting from row 2 to skip the header)
                for (int row = 2; row <= rowCount; row++)
                {
                    var cleantitle = worksheet.Cells[row, 1].Text;
                    string title = CleanTitle(cleantitle);  // Clean the title

                    // Check if the title exists in the Titles table (case-insensitive) using in-memory data
                    var existingTitle = allTitles
                        .FirstOrDefault(t => CleanTitle(t.Title) == title);

                    // Create a TitleValidationViewModel for the current row
                    var titleValidation = new TitleValidationViewModel
                    {
                        RowNumber = row,
                        Title = title,
                        DateAdded = DateTime.Now,
                        Status = existingTitle != null ? "Blocked" : "Clean"
                    };

                    // Add the titleValidation object to the appropriate list
                    if (existingTitle != null)
                    {
                        result.BlockedTitles.Add(titleValidation);  // Add to BlockedTitles if it already exists
                    }
                    else
                    {
                        result.CleanTitles.Add(titleValidation);  // Add to CleanTitles if it doesn't exist
                    }
                }
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
                worksheet.Cell(1, 1).Value = "TITLES";


                // Adjust column widths to fit content
                worksheet.Columns().AdjustToContents();

                // Optionally, apply styles to the header row for better visibility
                var headerRow = worksheet.Range("A1:L1");
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

        public string CleanTitle(string title)
        {
            // Convert to lowercase to make the comparison case-insensitive
            string cleanedTitle = title.ToLower();

            // Remove special characters (keep only letters and spaces)
            cleanedTitle = Regex.Replace(cleanedTitle, @"[^a-zA-Z0-9\s]", "");

            // Remove extra spaces
            cleanedTitle = Regex.Replace(cleanedTitle, @"\s+", " ").Trim();

            return cleanedTitle;
        }

        [HttpPost]
        [HttpPost]
        public IActionResult InsertCleanTitles(string cleanTitles)
        {
            // Check if the hidden input value is null or empty
            if (!string.IsNullOrEmpty(cleanTitles))
            {
                // Deserialize the JSON array into a list of strings
                var titlesList = JsonSerializer.Deserialize<List<string>>(cleanTitles);

                if (titlesList != null && titlesList.Any())
                {
                    // Iterate through each title and save it to the database
                    foreach (var title in titlesList)
                    {
                        var newTitle = new TitleValidationViewModel
                        {
                            RowNumber = 1, // Example row number; adjust as needed
                            Title = title, // Add the title
                            Status = "Clean", // Default status
                            DateAdded = DateTime.Now // Add the current timestamp
                        };

                        // Add the title to the database context
                        _context.Titles.Add(newTitle);
                    }

                    // Save changes to the database
                    _context.SaveChanges();
                }
            }

            // Redirect to the Index page after insertion
            return RedirectToAction("Index");
        }







    }
}
