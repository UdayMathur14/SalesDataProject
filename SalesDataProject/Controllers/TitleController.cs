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
        //public async Task<IActionResult> ViewTitles()
        //{
        //    try
        //    {
        //        var canAccessTitle = HttpContext.Session.GetString("CanViewTitles");
        //        var canDeleteTitle = HttpContext.Session.GetString("CanDeleteTitles");
        //        ViewData["CanViewTitles"] = canAccessTitle;
        //        ViewData["CanDeleteTitles"] = canDeleteTitle;
        //        var titles = await _context.Titles.AsNoTracking().ToListAsync();
        //        return View(titles);
        //    }
        //    catch (Exception ex)
        //    {
        //        return RedirectToAction("Login", "Auth");
        //    }
        //}

        public async Task<IActionResult> ViewTitles()
        {
            try
            {
                var canAccessTitle = HttpContext.Session.GetString("CanViewTitles");
                var canDeleteTitle = HttpContext.Session.GetString("CanDeleteTitles");

                if (string.IsNullOrEmpty(canAccessTitle))
                {
                    return RedirectToAction("Login", "Auth");
                }

                ViewData["CanViewTitles"] = canAccessTitle;
                ViewData["CanDeleteTitles"] = canDeleteTitle;

                var titles = await _context.Titles
    .AsNoTracking()
    .OrderByDescending(t => t.Id)
    .ToListAsync();// Use the correct property name.ToListAsync();

                return View(titles);
            }
            catch (Exception ex)
            {
                // Log ex.Message for debugging
                return RedirectToAction("Login", "Auth");
            }
        }


        [HttpPost]
        public async Task<IActionResult> UploadExcel(IFormFile file, bool testMode = false)
        {
            try
            {
                var username = HttpContext.Session.GetString("Username");
                if (string.IsNullOrEmpty(username))
                {
                    TempData["Message"] = "Session Expired, Please Login again";
                    TempData["MessageType"] = "Error";
                    return RedirectToAction("Login", "Auth");
                }

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
                        //if (string.IsNullOrWhiteSpace(invoiceNumber))
                        //{
                        //    hasInvalidInvoiceNumber = true; // Set the flag if an invalid row is found
                        //}

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
                        if (invoiceNumber == null || string.IsNullOrWhiteSpace(invoiceNumber))
                        {
                            duplicateTitlesInExcel.Add(concatenatedTitle);

                            result.DuplicateTitlesInExcel.Add(new TitleValidationViewModel
                            {
                                RowNumber = row,
                                Title = cleantitle,
                                InvoiceNumber = invoiceNumber,
                                CodeReference = codeReference,
                                Status = "Invoice No is Missing ",
                                TitleYear = yearTtile
                            });

                            continue; // Skip further processing for duplicates
                        }
                        if (codeReference == null || string.IsNullOrWhiteSpace(codeReference))
                        {
                            duplicateTitlesInExcel.Add(concatenatedTitle);

                            result.DuplicateTitlesInExcel.Add(new TitleValidationViewModel
                            {
                                RowNumber = row,
                                Title = cleantitle,
                                InvoiceNumber = invoiceNumber,
                                CodeReference = codeReference,
                                Status = "Code Reference No is Missing ",
                                TitleYear = yearTtile
                            });

                            continue; // Skip further processing for duplicates
                        }
                        if (!IsValidFinancialYear(yearTtile))
                        {
                            duplicateTitlesInExcel.Add(concatenatedTitle);

                            result.DuplicateTitlesInExcel.Add(new TitleValidationViewModel
                            {
                                RowNumber = row,
                                Title = cleantitle,
                                InvoiceNumber = invoiceNumber,
                                CodeReference = codeReference,
                                Status = "Invalid Financial Year",
                                TitleYear = yearTtile
                            });

                            continue; // Skip this row as the financial year is invalid
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

                        var isInvoiceExists = allTitles.Any(t => t.InvoiceNumber == invoiceNumber);

                        if (isInvoiceExists )
                        {
                            duplicateTitlesInExcel.Add(concatenatedTitle);

                            result.DuplicateTitlesInExcel.Add(new TitleValidationViewModel
                            {
                                RowNumber = row,
                                Title = cleantitle,
                                InvoiceNumber = invoiceNumber,
                                CodeReference = codeReference,
                                Status = "Invoice No already exists",
                                TitleYear = yearTtile
                            });

                            continue; // Skip this row and don't process further
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
                            BlockedByInvoiceNo = existingTitle?.InvoiceNumber,
                            BlockedCodeRef = existingTitle?.CodeReference
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
                if (!testMode)
                {
                    
                    var cleanRecordsToSave = result.CleanTitles
                        .Select(tv => new TitleValidationViewModel // <-- Use the correct entity, not the ViewModel
                        {
                            Title = tv.Title,
                            InvoiceNumber = tv.InvoiceNumber,
                            CodeReference = tv.CodeReference,
                            CREATED_ON = tv.CREATED_ON, // or tv.CREATED_ON if it's valid
                            CREATED_BY = username,
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
                    else
                    {
                        TempData["Message"] = "Successfully Uploaded";
                        TempData["MessageType"] = "Success";
                    }
                }
                else if (testMode)
                {
                    TempData["Message"] = "Test mode: Validation successful. No data saved.";
                    TempData["MessageType"] = "Info";
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
                var result = new ValidationResultViewModel
                {

                };
                TempData["Message"] = "An unexpected error occurred. Please try again.";
                TempData["MessageType"] = "Error";
                return View("Index",result);
            }
        }

        [HttpGet]
        public IActionResult DownloadTemplate()
        {
            try
            {
                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("UploadTitles");

                    // Define the headers
                    worksheet.Cell(1, 1).Value = "Invoice No (Required)";
                    worksheet.Cell(1, 2).Value = "Code Ref (Required)";
                    worksheet.Cell(1, 3).Value = "Title (Required)";
                    worksheet.Cell(1, 4).Value = "Financial Year (Required)";
                    worksheet.Cell(1, 5).Value = "Example";

                    // Apply style to header row
                    var headerRange = worksheet.Range("A1:E1");
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Font.FontColor = XLColor.Red;
                    headerRange.Style.Fill.BackgroundColor = XLColor.LightYellow; // Light background for visibility
                    headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    // Apply black border to header row
                    headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    headerRange.Style.Border.OutsideBorderColor = XLColor.Black;
                    headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                    headerRange.Style.Border.InsideBorderColor = XLColor.Black;

                    // Example row (second row)
                    worksheet.Cell(2, 1).Value = "INV123";
                    worksheet.Cell(2, 2).Value = "CR456";
                    worksheet.Cell(2, 3).Value = "Sample Title";
                    worksheet.Cell(2, 4).Value = "2025-26";
                    worksheet.Cell(2, 5).Value = "Example row. Please delete and follow this format.";


                    // Apply style to example row
                    var exampleRow = worksheet.Range("A2:E2");
                    exampleRow.Style.Font.FontColor = XLColor.Gray;
                    exampleRow.Style.Font.Italic = true;

                    // Set custom column widths (give space to look neat)
                    worksheet.Column(1).Width = 20; // Invoice No
                    worksheet.Column(2).Width = 20; // Code Ref
                    worksheet.Column(3).Width = 25; // Title
                    worksheet.Column(4).Width = 23;
                    worksheet.Column(5).Width = 40; // Example column

                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        var content = stream.ToArray();
                        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "UploadTitles.xlsx");
                    }
                }
            }
            catch (Exception ex)
            {
                var result = new ValidationResultViewModel();
                TempData["Message"] = "An unexpected error occurred. Please try again.";
                TempData["MessageType"] = "Error";
                return View("Index", result);
            }
        }


        private bool IsValidFinancialYear(string year)
        {
            var yearParts = year.Split('-');

            if (yearParts.Length != 2) return false;

            if (!int.TryParse(yearParts[0], out int startYear) || !int.TryParse(yearParts[1], out int endYearPart))
                return false;

            // Ensure start year is in valid range
            if (startYear < 1999 || startYear > 2099) return false;

            // Get the expected last two digits of (startYear + 1)
            int expectedEndYearPart = (startYear + 1) % 100;

            return endYearPart == expectedEndYearPart;
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
                return View("ViewTitles");
            }
        }

        //public async Task<IActionResult> querydata(string filterId, string filterCodeReference, string filterInvoiceNumber,string titleYear)
        //{
        //    // Pass filters back to the view
        //    ViewData["FilterId"] = filterId;
        //    ViewData["FilterCodeReference"] = filterCodeReference;
        //    ViewData["FilterInvoiceNumber"] = filterInvoiceNumber;
        //    ViewData["TitleYear"] = titleYear;

        //    // Fetch data and filter based on inputs
        //    var query = _context.Titles.AsQueryable();

        //    if (!string.IsNullOrEmpty(filterId) && int.TryParse(filterId, out int id))
        //    {
        //        query = query.Where(x => x.Id == id);
        //    }

        //    if (!string.IsNullOrEmpty(filterCodeReference))
        //    {
        //        query = query.Where(x => x.CodeReference.Contains(filterCodeReference));
        //    }

        //    if (!string.IsNullOrEmpty(filterInvoiceNumber))
        //    {
        //        query = query.Where(x => x.InvoiceNumber.Contains(filterInvoiceNumber));
        //    }

        //    if (!string.IsNullOrEmpty(titleYear))
        //    {
        //        query = query.Where(x => x.TitleYear.Contains(titleYear));
        //    }

        //    var canDeleteTitle = HttpContext.Session.GetString("CanDeleteTitles");
        //    ViewData["CanDeleteTitles"] = canDeleteTitle;


        //    var model = await query.ToListAsync();
        //    return View("ViewTitles", model);
        //}

        public async Task<IActionResult> querydata(string filterId, string filterCodeReference, string filterInvoiceNumber, string titleYear)
        {
            // Filtering logic as before
            var query = _context.Titles.AsQueryable();

            if (!string.IsNullOrEmpty(filterId) && int.TryParse(filterId, out int id))
            {
                query = query.Where(x => x.Id == id);
            }

            if (!string.IsNullOrEmpty(filterCodeReference))
            {
                query = query.Where(x => x.CodeReference.Contains(filterCodeReference));
            }

            if (!string.IsNullOrEmpty(filterInvoiceNumber))
            {
                query = query.Where(x => x.InvoiceNumber.Contains(filterInvoiceNumber));
            }

            if (!string.IsNullOrEmpty(titleYear))
            {
                query = query.Where(x => x.TitleYear.Contains(titleYear));
            }

            var canDeleteTitle = HttpContext.Session.GetString("CanDeleteTitles");
            ViewData["CanDeleteTitles"] = canDeleteTitle;

            var model = await query.ToListAsync();
            return View("ViewTitles", model);
        }


        [HttpGet]
        public IActionResult GetDropdownData()
        {
            var codeReferences = _context.Titles
                                          .Where(x => !string.IsNullOrEmpty(x.CodeReference))
                                          .Select(x => x.CodeReference)
                                          .Distinct()
                                          .ToList();

            var invoiceNumbers = _context.Titles
                                          .Where(x => !string.IsNullOrEmpty(x.InvoiceNumber))
                                          .Select(x => x.InvoiceNumber)
                                          .Distinct()
                                          .ToList();

            

            return Json(new { codeReferences, invoiceNumbers });
        }

        public async Task<IActionResult> DownloadExcel()
        {
            try
            {
                var titles = await _context.Titles.ToListAsync();

                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("Titles");

                    // Add Header
                    worksheet.Cells[1, 1].Value = "Id";
                    worksheet.Cells[1, 2].Value = "Code Ref";
                    worksheet.Cells[1, 3].Value = "Invoice No";
                    worksheet.Cells[1, 4].Value = "Title";
                    worksheet.Cells[1, 5].Value = "Created By";
                    worksheet.Cells[1, 6].Value = "Year";
                    worksheet.Cells[1, 7].Value = "Status";

                    int row = 2;
                    foreach (var record in titles)
                    {
                        worksheet.Cells[row, 1].Value = record.Id;
                        worksheet.Cells[row, 2].Value = record.CodeReference;
                        worksheet.Cells[row, 3].Value = record.InvoiceNumber;
                        worksheet.Cells[row, 4].Value = record.Title;
                        worksheet.Cells[row, 5].Value = record.CREATED_BY;
                        worksheet.Cells[row, 6].Value = record.TitleYear;
                        worksheet.Cells[row, 7].Value = record.Status;
                        row++;
                    }
                    worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
                    var stream = new MemoryStream();
                    package.SaveAs(stream);
                    stream.Position = 0;
                    
                    string excelName = $"TitleRecords-{DateTime.Now:yyyyMMddHHmmss}.xlsx";
                    return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", excelName);
                }
            }
            catch (Exception ex)
            {
                return RedirectToAction("ViewTitles");
            }
        }


    }
}
