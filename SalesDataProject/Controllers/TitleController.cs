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
            try {
                var canAccessTitle = HttpContext.Session.GetString("CanViewTitles");
                var canDeleteTitle = HttpContext.Session.GetString("CanDeleteTitles");
                ViewData["CanViewTitles"] = canAccessTitle;
                ViewData["CanDeleteTitles"] = canDeleteTitle;
                return View(model);
            }
            catch (Exception ex)
            {

                return RedirectToAction("Login", "Auth");
            }
        }
        public async Task<IActionResult> ViewTitles()
        {
            try
            {
                var canAccessTitle = HttpContext.Session.GetString("CanViewTitles");
                var canDeleteTitle = HttpContext.Session.GetString("CanDeleteTitles");
                ViewData["CanViewTitles"] = canAccessTitle;
                ViewData["CanDeleteTitles"] = canDeleteTitle;
                var titles = await _context.Titles.ToListAsync();
                return View(titles);
            }
            catch (Exception ex)
            {

                return RedirectToAction("Login", "Auth");
            }
        }

        [HttpPost]
        public async Task<IActionResult> UploadExcel(IFormFile file)
        {
            try
            {
                var username = HttpContext.Session.GetString("Username");
                ViewBag.Username = username;
               
                if (file == null || file.Length == 0)
                {
                    return BadRequest("No file uploaded.");
                }

                var result = new ValidationResultViewModel(); // Initialize result
                bool hasInvalidInvoiceNumber = false; // Flag to check for invalid rows

                var duplicateTitlesInExcel = new HashSet<string>(); // To track duplicates within the Excel file
                var titlesInExcel = new HashSet<string>(); // To store unique titles within the Excel file

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
                        var yearTtile = worksheet.Cells[row, 4].Text;

                        if (string.IsNullOrWhiteSpace(cleantitle))
                        {
                            // Assume that an empty Title indicates the end of the valid rows
                            break; // Exit the loop when an empty Title is encountered
                        }

                        // Clean and concatenate the title
                        string concatenatedTitle = CleanTitle(cleantitle);

                        // Check if the InvoiceNumber is empty or null
                        if (string.IsNullOrWhiteSpace(invoiceNumber))
                        {
                            hasInvalidInvoiceNumber = true; // Set the flag if an invalid row is found
                        }

                        if (yearTtile == null || string.IsNullOrWhiteSpace(yearTtile))
                        {
                            duplicateTitlesInExcel.Add(concatenatedTitle);

                            result.DuplicateTitlesInExcel.Add(new TitleValidationViewModel
                            {
                                RowNumber = row,
                                Title = cleantitle,
                                InvoiceNumber = invoiceNumber,
                                CodeReference = codeReference,
                                Status = "Year Missing ",
                                TitleYear = yearTtile
                            });

                            continue; // Skip further processing for duplicates
                        }
                        // Check for duplicate titles within the Excel file
                        if (titlesInExcel.Contains(concatenatedTitle))
                        {
                            duplicateTitlesInExcel.Add(concatenatedTitle);

                            result.DuplicateTitlesInExcel.Add(new TitleValidationViewModel
                            {
                                RowNumber = row,
                                Title = cleantitle,
                                InvoiceNumber = invoiceNumber,
                                CodeReference = codeReference,
                                Status = "Duplicate in Excel",
                                TitleYear = yearTtile
                            });

                            continue; // Skip further processing for duplicates
                        }
                        else
                        {
                            titlesInExcel.Add(concatenatedTitle);
                        }

                        // Check if the concatenated title matches any ReferenceTitle in the database
                        var existingTitle = allTitles
                            .FirstOrDefault(t => t.ReferenceTitle == concatenatedTitle);

                        // Create a TitleValidationViewModel for the current row
                        var titleValidation = new TitleValidationViewModel
                        {
                            RowNumber = row,
                            Title = cleantitle,
                            InvoiceNumber = invoiceNumber,
                            CodeReference = codeReference,
                            CREATED_ON = DateOnly.FromDateTime(DateTime.Now),
                            CREATED_BY = username,
                            Status = existingTitle != null ? "Blocked" : "Clean",
                            ReferenceTitle = concatenatedTitle,
                            BlockedId = existingTitle?.Id,
                            TitleYear = yearTtile,
                            // Only populate BlockedByInvoiceNo and BlockedCodeRef if the title is blocked
                            BlockedByInvoiceNo = existingTitle != null ? invoiceNumber : null,
                            BlockedCodeRef = existingTitle != null ? codeReference : null
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
                    var cleanRecordsToSave = result.CleanTitles
                        .Select(tv => new TitleValidationViewModel
                        {
                            Title = tv.Title,
                            InvoiceNumber = tv.InvoiceNumber,
                            CodeReference = tv.CodeReference,
                            CREATED_ON = tv.CREATED_ON,
                            CREATED_BY = tv.CREATED_BY,
                            ReferenceTitle = CleanTitle(tv.Title),
                            Status = "Clean",
                            TitleYear = tv.TitleYear

                        }).ToList();

                    if (cleanRecordsToSave.Any())
                    {
                        _context.Titles.AddRange(cleanRecordsToSave);
                        await _context.SaveChangesAsync();
                        TempData["Message"] = "Successfully Saved";
                        TempData["MessageType"] = "Success";
                    }
                }
                else
                {
                    TempData["Message"] = "One or more rows have an empty Invoice Number. No data has been saved to the database.";
                    TempData["MessageType"] = "Error";
                }
                var canAccessTitle = HttpContext.Session.GetString("CanViewTitles");
                var canDeleteTitle = HttpContext.Session.GetString("CanDeleteTitles");
                ViewData["CanViewTitles"] = canAccessTitle;
                ViewData["CanDeleteTitles"] = canDeleteTitle;
                // Return the result to the view
                return View("Index", result);
            }
            catch (Exception ex)
            {
                TempData["Message"] = "An unexpected error occurred. Please try again.";
                TempData["MessageType"] = "Error";
                return View("Index");
            }
        }




        [HttpGet]
        public IActionResult DownloadTemplate()
        {
            try
            {
                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Titles");

                    // Define the headers in the template
                    worksheet.Cell(1, 1).Value = "InvoiceNumber";
                    worksheet.Cell(1, 2).Value = "CodeReference";
                    worksheet.Cell(1, 3).Value = "*Title";
                    worksheet.Cell(1, 4).Value = "*Year";

                    //worksheet.Cell(2, 1).Value = "ExINV001";
                    //worksheet.Cell(2, 2).Value = "Ex1234";
                    //worksheet.Cell(2, 3).Value = "UploadTitle";
                    //worksheet.Cell(2, 4).Value = "2025";
                    

                    // Set the column width specifically for the "Title" column
                    worksheet.Column(3).Width = 11.0; // Approximate width for 3 cm

                    // Optionally, adjust the other columns to fit content
                    worksheet.Column(1).AdjustToContents();
                    worksheet.Column(2).AdjustToContents();

                   

                    // Optionally, apply styles to the header row for better visibility
                    var headerRow = worksheet.Range("A1:D1");
                    headerRow.Style.Font.Bold = true;
                    headerRow.Style.Font.FontColor = XLColor.Red;

                    var row = worksheet.Range("A2:L2");
                    row.Style.Font.FontColor = XLColor.Black;

                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        var content = stream.ToArray();
                        TempData["Message"] = "Succesfully downloaded";
                        TempData["MessageType"] = "Error";
                        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "UploadTitles.xlsx");
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["Message"] = "An unexpected error occurred. Please try again.";
                TempData["MessageType"] = "Error";
                return View("Index");
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

        [HttpPost]
        public async Task<IActionResult> DeleteSelected(List<int> selectedIds)
        {
            try
            {


                if (selectedIds == null || !selectedIds.Any())
                {
                    TempData["Message"] = "No records selected for deletion.";
                    TempData["MessageType"] = "Error";
                    return RedirectToAction("ViewTitles");
                }

                var titlesToDelete = _context.Titles.Where(t => selectedIds.Contains(t.Id)).ToList();

                if (titlesToDelete.Any())
                {
                    _context.Titles.RemoveRange(titlesToDelete);
                    await _context.SaveChangesAsync();
                    TempData["Message"] = "Selected records deleted successfully.";
                    TempData["MessageType"] = "Success";
                }
                else
                {
                    TempData["Message"] = "No matching records found for deletion.";
                    TempData["MessageType"] = "Success";
                }

                return RedirectToAction("ViewTitles");
            }
            catch (Exception ex)
            {
                TempData["Message"] = "An unexpected error occurred. Please try again.";
                TempData["MessageType"] = "Error";
                return RedirectToAction("ViewTitles");
            }
        }


        public async Task<IActionResult> querydata(string filterId, string filterCodeReference, string filterTitle)
        {
            // Pass filters back to the view
            ViewData["FilterId"] = filterId;
            ViewData["FilterCodeReference"] = filterCodeReference;
            ViewData["FilterTitle"] = filterTitle;

            // Fetch data and filter based on inputs
            var query = _context.Titles.AsQueryable();

            if (!string.IsNullOrEmpty(filterId) && int.TryParse(filterId, out int id))
            {
                query = query.Where(x => x.Id == id);
            }

            if (!string.IsNullOrEmpty(filterCodeReference))
            {
                query = query.Where(x => x.CodeReference.Contains(filterCodeReference));
            }

            if (!string.IsNullOrEmpty(filterTitle))
            {
                query = query.Where(x => x.Title.Contains(filterTitle));
            }

            var model = await query.ToListAsync();

            return View("ViewTitles", model);
        }


    }
}
