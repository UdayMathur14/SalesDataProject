using ClosedXML.Excel;
using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.InkML;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using OfficeOpenXml;
using SalesDataProject.Models;
using System.Drawing.Printing;
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
            var username = HttpContext.Session.GetString("Username");

            if (string.IsNullOrWhiteSpace(username) || username == null || username == "")
            {
                TempData["Message"] = "Session Expired";
                TempData["MessageType"] = "Error";
                return RedirectToAction("Login", "Auth");
            }
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
        public async Task<IActionResult> ViewTitles(int page = 1, int pageSize = 100)
        {
            var username = HttpContext.Session.GetString("Username");

            if (string.IsNullOrWhiteSpace(username))
            {
                TempData["Message"] = "Session Expired, Please Login again";
                TempData["MessageType"] = "Error";
                return RedirectToAction("Login", "Auth");
            }

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

                // Count total records for pagination
                var totalRecords = await _context.Titles.CountAsync();

                // Fetch paginated data
                var titles = await _context.Titles
                    .AsNoTracking()
                    .OrderByDescending(t => t.Id)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // Pagination metadata
                ViewBag.CurrentPage = page;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize);
                ViewData["FilteredCount"] = totalRecords; // Syncing with your existing UI

                return View(titles);
            }
            catch (Exception ex)
            {
                return RedirectToAction("Login", "Auth");
            }
        }

        public async Task<IActionResult> ModifiedTitles(int page = 1, int pageSize = 100)
        {
            var username = HttpContext.Session.GetString("Username");

            //if (string.IsNullOrWhiteSpace(username))
            //{
            //    TempData["Message"] = "Session Expired, Please Login again";
            //    TempData["MessageType"] = "Error";
            //    return RedirectToAction("Login", "Auth");
            //}
            try
            {
                //var canAccessTitle = HttpContext.Session.GetString("CanViewTitles");

                //if (string.IsNullOrEmpty(canAccessTitle))
                //{
                //    return RedirectToAction("Login", "Auth");
                //}

                //ViewData["CanViewTitles"] = canAccessTitle;

                // Count total records for pagination
                var totalRecords = await _context.Titles.Where(a=>a.UpdatedTitle!=null).CountAsync();

                // Fetch paginated data
                var titles = await _context.Titles.Where(a => a.UpdatedTitle != null)
                    .AsNoTracking()
                    .OrderByDescending(t => t.Id)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // Pagination metadata
                ViewBag.CurrentPage = page;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize);
                ViewData["FilteredCount"] = totalRecords; // Syncing with your existing UI

                return View(titles);
            }
            catch (Exception ex)
            {
                return RedirectToAction("Login", "Auth");
            }
        }

        //public async Task<IActionResult> UploadExcel(IFormFile file, bool testMode = false)
        //{
        //    try
        //    {
        //        var username = HttpContext.Session.GetString("Username");
        //        if (string.IsNullOrEmpty(username))
        //        {
        //            TempData["Message"] = "Session Expired, Please Login again";
        //            TempData["MessageType"] = "Error";
        //            return RedirectToAction("Login", "Auth");
        //        }

        //        ViewBag.Username = username;

        //        if (file == null || file.Length == 0)
        //        {
        //            return BadRequest("No file uploaded.");
        //        }

        //        var result = new ValidationResultViewModel(); // Initialize result
        //        bool hasInvalidInvoiceNumber = false; // Flag to check for invalid rows

        //        var duplicateTitlesInExcel = new HashSet<string>(); // To track duplicates within the Excel file
        //        var titlesInExcel = new HashSet<string>(); // To store unique titles within the Excel file

        //        using (var package = new ExcelPackage(file.OpenReadStream()))
        //        {
        //            var worksheet = package.Workbook.Worksheets[0];
        //            var rowCount = worksheet.Dimension.Rows;

        //            // Fetch all Titles from the database
        //            var allTitles = await _context.Titles.ToListAsync();

        //            // Loop through each row in the worksheet (starting from row 2 to skip the header)
        //            for (int row = 2; row <= rowCount; row++)
        //            {
        //                var invoiceNumber = worksheet.Cells[row, 1].Text;
        //                var paperId = worksheet.Cells[row, 2].Text;
        //                var codeReference = worksheet.Cells[row, 3].Text;
        //                var cleantitle = worksheet.Cells[row, 4].Text;
        //                var yearTtile = worksheet.Cells[row, 5].Text;

        //                if (string.IsNullOrWhiteSpace(cleantitle))
        //                {
        //                    // Assume that an empty Title indicates the end of the valid rows
        //                    break; // Exit the loop when an empty Title is encountered
        //                }

        //                // Clean and concatenate the title
        //                string concatenatedTitle = CleanTitle(cleantitle);

        //                // Check if the InvoiceNumber is empty or null
        //                //if (string.IsNullOrWhiteSpace(invoiceNumber))
        //                //{
        //                //    hasInvalidInvoiceNumber = true; // Set the flag if an invalid row is found
        //                //}

        //                if (yearTtile == null || string.IsNullOrWhiteSpace(yearTtile))
        //                {
        //                    duplicateTitlesInExcel.Add(concatenatedTitle);

        //                    result.DuplicateTitlesInExcel.Add(new TitleValidationViewModel
        //                    {
        //                        RowNumber = row,
        //                        Title = cleantitle,
        //                        InvoiceNumber = invoiceNumber,
        //                        PaperId = paperId,
        //                        CodeReference = codeReference,
        //                        Status = "Year Missing ",
        //                        TitleYear = yearTtile
        //                    });

        //                    continue; // Skip further processing for duplicates
        //                }
        //                if (invoiceNumber == null || string.IsNullOrWhiteSpace(invoiceNumber))
        //                {
        //                    duplicateTitlesInExcel.Add(concatenatedTitle);

        //                    result.DuplicateTitlesInExcel.Add(new TitleValidationViewModel
        //                    {
        //                        RowNumber = row,
        //                        Title = cleantitle,
        //                        InvoiceNumber = invoiceNumber,
        //                        PaperId = paperId,
        //                        CodeReference = codeReference,
        //                        Status = "Invoice No is Missing ",
        //                        TitleYear = yearTtile
        //                    });

        //                    continue; // Skip further processing for duplicates
        //                }
        //                if (codeReference == null || string.IsNullOrWhiteSpace(codeReference))
        //                {
        //                    duplicateTitlesInExcel.Add(concatenatedTitle);

        //                    result.DuplicateTitlesInExcel.Add(new TitleValidationViewModel
        //                    {
        //                        RowNumber = row,
        //                        Title = cleantitle,
        //                        InvoiceNumber = invoiceNumber,
        //                        PaperId = paperId,
        //                        CodeReference = codeReference,
        //                        Status = "Code Reference No is Missing ",
        //                        TitleYear = yearTtile
        //                    });

        //                    continue; // Skip further processing for duplicates
        //                }
        //                if (!IsValidFinancialYear(yearTtile))
        //                {
        //                    duplicateTitlesInExcel.Add(concatenatedTitle);

        //                    result.DuplicateTitlesInExcel.Add(new TitleValidationViewModel
        //                    {
        //                        RowNumber = row,
        //                        Title = cleantitle,
        //                        InvoiceNumber = invoiceNumber,
        //                        PaperId = paperId,
        //                        CodeReference = codeReference,
        //                        Status = "Invalid Financial Year",
        //                        TitleYear = yearTtile
        //                    });

        //                    continue; // Skip this row as the financial year is invalid
        //                }
        //                // Check for duplicate titles within the Excel file
        //                if (titlesInExcel.Contains(concatenatedTitle))
        //                {
        //                    duplicateTitlesInExcel.Add(concatenatedTitle);

        //                    result.DuplicateTitlesInExcel.Add(new TitleValidationViewModel
        //                    {
        //                        RowNumber = row,
        //                        Title = cleantitle,
        //                        InvoiceNumber = invoiceNumber,
        //                        PaperId = paperId,
        //                        CodeReference = codeReference,
        //                        Status = "Duplicate in Excel",
        //                        TitleYear = yearTtile
        //                    });

        //                    continue; // Skip further processing for duplicates
        //                }
        //                else
        //                {
        //                    titlesInExcel.Add(concatenatedTitle);
        //                }

        //                //var isInvoiceExists = allTitles.Any(t => t.InvoiceNumber == invoiceNumber);
        //                var isInvoiceExists = allTitles.Any(t =>t.InvoiceNumber == invoiceNumber && t.CodeReference == codeReference && t.TitleYear == yearTtile);

        //                if (isInvoiceExists )
        //                {
        //                    duplicateTitlesInExcel.Add(concatenatedTitle);

        //                    result.DuplicateTitlesInExcel.Add(new TitleValidationViewModel
        //                    {
        //                        RowNumber = row,
        //                        Title = cleantitle,
        //                        InvoiceNumber = invoiceNumber,
        //                        PaperId = paperId,
        //                        CodeReference = codeReference,
        //                        Status = "Invoice with codeRef already exists",
        //                        TitleYear = yearTtile
        //                    });

        //                    continue; // Skip this row and don't process further
        //                }
        //                // Check if the concatenated title matches any ReferenceTitle in the database
        //                var existingTitle = allTitles
        //                    .FirstOrDefault(t => t.ReferenceTitle == concatenatedTitle);

        //                var existPaperID = allTitles
        //                    .FirstOrDefault(t => t.PaperId == paperId);

        //                // Create a TitleValidationViewModel for the current row
        //                var titleValidation = new TitleValidationViewModel
        //                {
        //                    RowNumber = row,
        //                    Title = cleantitle,
        //                    InvoiceNumber = invoiceNumber,
        //                    PaperId = paperId,
        //                    CodeReference = codeReference,
        //                    CREATED_ON = DateOnly.FromDateTime(DateTime.Now),
        //                    CREATED_BY = username,
        //                    Status = existingTitle != null ? "Blocked" : "Clean",
        //                    ReferenceTitle = concatenatedTitle,
        //                    BlockedId = existingTitle?.Id,
        //                    TitleYear = yearTtile,
        //                    // Only populate BlockedByInvoiceNo and BlockedCodeRef if the title is blocked
        //                    BlockedByInvoiceNo = existingTitle?.InvoiceNumber,
        //                    BlockedCodeRef = existingTitle?.CodeReference
        //                };

        //                // Add the titleValidation object to the appropriate list
        //                if (existingTitle != null && existPaperID!=null)
        //                {
        //                    result.BlockedTitles.Add(titleValidation); // Add to BlockedTitles if it already exists
        //                }
        //                else
        //                {
        //                    result.CleanTitles.Add(titleValidation); // Add to CleanTitles if it doesn't exist
        //                }
        //            }
        //        }
        //        // Save only clean records to the database if all rows have valid InvoiceNumbers
        //        if (!testMode)
        //        {

        //            var cleanRecordsToSave = result.CleanTitles
        //                .Select(tv => new TitleValidationViewModel // <-- Use the correct entity, not the ViewModel
        //                {
        //                    Title = tv.Title,
        //                    InvoiceNumber = tv.InvoiceNumber,
        //                    PaperId = tv.PaperId,
        //                    CodeReference = tv.CodeReference,
        //                    CREATED_ON = tv.CREATED_ON, // or tv.CREATED_ON if it's valid
        //                    CREATED_BY = username,
        //                    ReferenceTitle = CleanTitle(tv.Title),
        //                    Status = "Clean",
        //                    TitleYear = tv.TitleYear
        //                }).ToList();

        //            if (cleanRecordsToSave.Any())
        //            {
        //                _context.Titles.AddRange(cleanRecordsToSave);
        //                await _context.SaveChangesAsync();
        //                TempData["Message"] = "Successfully Saved";
        //                TempData["MessageType"] = "Success";
        //            }
        //            else
        //            {
        //                TempData["Message"] = "Successfully Uploaded";
        //                TempData["MessageType"] = "Success";
        //            }
        //        }
        //        else if (testMode)
        //        {
        //            TempData["Message"] = "Test mode: Validation successful. No data saved.";
        //            TempData["MessageType"] = "Info";
        //        }
        //        else
        //        {
        //            TempData["Message"] = "One or more rows have an empty Invoice Number. No data has been saved to the database.";
        //            TempData["MessageType"] = "Error";
        //        }
        //        var canAccessTitle = HttpContext.Session.GetString("CanViewTitles");
        //        var canDeleteTitle = HttpContext.Session.GetString("CanDeleteTitles");
        //        ViewData["CanViewTitles"] = canAccessTitle;
        //        ViewData["CanDeleteTitles"] = canDeleteTitle;
        //        // Return the result to the view
        //        return View("Index", result);
        //    }
        //    catch (Exception ex)
        //    {
        //        var result = new ValidationResultViewModel
        //        {

        //        };
        //        TempData["Message"] = "An unexpected error occurred. Please try again.";
        //        TempData["MessageType"] = "Error";
        //        return View("Index",result);
        //    }
        //}

        // Make sure to have this for Session serialization
        [HttpPost]
        public async Task<IActionResult> UploadExcel(IFormFile file, bool testMode = false, int page = 1, int pageSize = 50)
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
            ValidationResultViewModel result;

            // Agar user naya file upload kar raha hai (Page 1)
            if (file != null && file.Length > 0)
            {
                result = new ValidationResultViewModel();
                var titlesInExcel = new HashSet<string>();
                var allTitles = await _context.Titles.ToListAsync();

                using (var package = new ExcelPackage(file.OpenReadStream()))
                {
                    var worksheet = package.Workbook.Worksheets[0];
                    var rowCount = worksheet.Dimension?.Rows ?? 0;

                    for (int row = 2; row <= rowCount; row++)
                    {
                        var invoiceNumber = worksheet.Cells[row, 1].Text?.Trim();
                        var paperId = worksheet.Cells[row, 2].Text?.Trim();
                        var codeReference = worksheet.Cells[row, 3].Text?.Trim();
                        var cleantitle = worksheet.Cells[row, 4].Text?.Trim();
                        var yearTtile = worksheet.Cells[row, 5].Text?.Trim();

                        if (string.IsNullOrWhiteSpace(cleantitle)) break;

                        string concatenatedTitle = CleanTitle(cleantitle);

                        var titleValidation = new TitleValidationViewModel
                        {
                            RowNumber = row,
                            Title = cleantitle,
                            InvoiceNumber = invoiceNumber,
                            PaperId = paperId,
                            CodeReference = codeReference,
                            TitleYear = yearTtile,
                            CREATED_ON = DateOnly.FromDateTime(DateTime.Now),
                            CREATED_BY = username,
                            ReferenceTitle = concatenatedTitle
                        };

                        // 1. Validation Logic
                        if (string.IsNullOrWhiteSpace(yearTtile))
                        {
                            titleValidation.Status = "Year Missing";
                            result.DuplicateTitlesInExcel.Add(titleValidation);
                            continue;
                        }
                        if (string.IsNullOrWhiteSpace(invoiceNumber))
                        {
                            titleValidation.Status = "Invoice No is Missing";
                            result.DuplicateTitlesInExcel.Add(titleValidation);
                            continue;
                        }
                        if (string.IsNullOrWhiteSpace(codeReference))
                        {
                            titleValidation.Status = "Code Reference No is Missing";
                            result.DuplicateTitlesInExcel.Add(titleValidation);
                            continue;
                        }
                        if (!IsValidFinancialYear(yearTtile))
                        {
                            titleValidation.Status = "Invalid Financial Year";
                            result.DuplicateTitlesInExcel.Add(titleValidation);
                            continue;
                        }

                        // 2. Duplicate Check in Excel
                        if (titlesInExcel.Contains(concatenatedTitle))
                        {
                            titleValidation.Status = "Duplicate in Excel";
                            result.DuplicateTitlesInExcel.Add(titleValidation);
                            continue;
                        }
                        titlesInExcel.Add(concatenatedTitle);

                        // 3. Database Check
                        var isInvoiceExists = allTitles.Any(t => t.InvoiceNumber == invoiceNumber && t.CodeReference == codeReference && t.TitleYear == yearTtile);
                        if (isInvoiceExists)
                        {
                            titleValidation.Status = "Invoice with codeRef already exists";
                            result.DuplicateTitlesInExcel.Add(titleValidation);
                            continue;
                        }

                            //var existingTitle = allTitles.FirstOrDefault(t => t.ReferenceTitle == concatenatedTitle);
                            var existingTitle = allTitles.FirstOrDefault(t =>
    (
        !string.IsNullOrWhiteSpace(t.UpdatedReferenceTitle)
            ? t.UpdatedReferenceTitle
            : t.ReferenceTitle
    ) == concatenatedTitle
);
                            var existPaperID = allTitles.FirstOrDefault(t => t.PaperId == paperId);

                        if (existingTitle != null && existPaperID != null)
                        {
                            titleValidation.Status = "Blocked";
                            titleValidation.BlockedId = existingTitle.Id;
                            titleValidation.BlockedByInvoiceNo = existingTitle.InvoiceNumber;
                            titleValidation.BlockedCodeRef = existingTitle.CodeReference;
                            result.BlockedTitles.Add(titleValidation);
                        }
                        else
                        {
                            titleValidation.Status = "Clean";
                            result.CleanTitles.Add(titleValidation);
                        }
                    }
                }

                // Save Clean Records if NOT in test mode
                if (!testMode && result.CleanTitles.Any())
                {
                    var cleanRecordsToSave = result.CleanTitles.Select(tv => new TitleValidationViewModel // Replace 'Title' with your actual Entity name
                    {
                        Title = tv.Title, // Adjust property names to match your DB Entity
                        InvoiceNumber = tv.InvoiceNumber,
                        PaperId = tv.PaperId,
                        CodeReference = tv.CodeReference,
                        CREATED_ON = tv.CREATED_ON,
                        CREATED_BY = username,
                        ReferenceTitle = tv.ReferenceTitle,
                        Status = "Clean",
                        TitleYear = tv.TitleYear
                    }).ToList();

                    _context.Titles.AddRange(cleanRecordsToSave);
                    await _context.SaveChangesAsync();
                    TempData["Message"] = "Successfully Saved";
                    TempData["MessageType"] = "Success";
                }
                else
                {
                    TempData["Message"] = testMode ? "Test mode: Validation successful." : "Upload Completed";
                    TempData["MessageType"] = testMode ? "Info" : "Success";
                }

                // Store result in session for pagination
                var settings = new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore };
                HttpContext.Session.SetString("UploadResult", JsonConvert.SerializeObject(result, settings));
            }
            else
            {
                // Pagination request: Retrieve from session
                var sessionData = HttpContext.Session.GetString("UploadResult");
                if (string.IsNullOrEmpty(sessionData)) return RedirectToAction("Index");
                result = JsonConvert.DeserializeObject<ValidationResultViewModel>(sessionData);
            }

            // --- Pagination Logic ---
            int skip = (page - 1) * pageSize;

            // We create a "Paged" version of the result to send to the View
            var pagedResult = new ValidationResultViewModel();

            pagedResult.CleanTitles = result.CleanTitles.Skip(skip).Take(pageSize).ToList();
            pagedResult.BlockedTitles = result.BlockedTitles.Skip(skip).Take(pageSize).ToList();
            pagedResult.DuplicateTitlesInExcel = result.DuplicateTitlesInExcel.Skip(skip).Take(pageSize).ToList();

            // Meta data for View
            int maxRows = Math.Max(result.CleanTitles.Count, Math.Max(result.BlockedTitles.Count, result.DuplicateTitlesInExcel.Count));
            ViewBag.TotalPages = (int)Math.Ceiling((double)maxRows / pageSize);
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;

            var canAccessTitle = HttpContext.Session.GetString("CanViewTitles");
            var canDeleteTitle = HttpContext.Session.GetString("CanDeleteTitles");
            ViewData["CanViewTitles"] = canAccessTitle;
            ViewData["CanDeleteTitles"] = canDeleteTitle;

            return View("Index", pagedResult);
        }
        catch (Exception ex)
        {
            TempData["Message"] = "An error occurred: " + ex.Message;
            TempData["MessageType"] = "Error";
            return View("Index", new ValidationResultViewModel());
        }
    }

        [HttpPost]
        public async Task<IActionResult> ModifiedTitleExcel(IFormFile file, int page = 1, int pageSize = 50)
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

                ValidationResultViewModel result = new ValidationResultViewModel();

                if (file != null && file.Length > 0)
                {
                    var allTitles = await _context.Titles.ToListAsync();

                    // 🔥 FAST LOOKUP (PaperId)
                    var titleDict = allTitles.ToDictionary(t => t.PaperId);

                    // 🔥 EXISTING CLEAN TITLES SET
                    var titleSet = new HashSet<string>(
                        allTitles.Select(t =>
                            CleanTitle(!string.IsNullOrWhiteSpace(t.UpdatedReferenceTitle)
                                ? t.UpdatedReferenceTitle
                                : t.ReferenceTitle))
                    );

                    using (var package = new ExcelPackage(file.OpenReadStream()))
                    {
                        var worksheet = package.Workbook.Worksheets[0];
                        var rowCount = worksheet.Dimension?.Rows ?? 0;

                        for (int row = 2; row <= rowCount; row++)
                        {
                            var paperId = worksheet.Cells[row, 1].Text?.Trim();
                            var updatedTitle = worksheet.Cells[row, 2].Text?.Trim();

                            if (string.IsNullOrWhiteSpace(paperId) || string.IsNullOrWhiteSpace(updatedTitle))
                                continue;

                            string cleanTitle = CleanTitle(updatedTitle);

                            // 🔥 1. PaperId must exist
                            if (!titleDict.TryGetValue(paperId, out var existingRecord))
                            {
                                result.DuplicateTitlesInExcel.Add(new TitleValidationViewModel
                                {
                                    RowNumber = row,
                                    PaperId = paperId,
                                    UpdatedTitle = updatedTitle
                                });
                                continue;
                            }

                            // 🔥 2. Duplicate check (DB + runtime)
                            if (titleSet.Contains(cleanTitle))
                            {
                                result.DuplicateTitlesInExcel.Add(new TitleValidationViewModel
                                {
                                    RowNumber = row,
                                    PaperId = paperId,
                                    UpdatedTitle = updatedTitle
                                });
                                continue;
                            }

                            // ✅ UPDATE
                            existingRecord.UpdatedTitle = updatedTitle;
                            existingRecord.UpdatedReferenceTitle = cleanTitle;
                            existingRecord.UpdatedTitleBy = username;

                            // 🔥 VERY IMPORTANT (runtime duplicate avoid)
                            titleSet.Add(cleanTitle);

                            result.CleanTitles.Add(new TitleValidationViewModel
                            {
                                RowNumber = row,
                                PaperId = paperId,
                                UpdatedTitle = updatedTitle,
                                UpdatedTitleBy = username
                            });
                        }
                    }

                    // ✅ SAVE
                    if (result.CleanTitles.Any())
                    {
                        await _context.SaveChangesAsync();
                        TempData["Message"] = "Titles Updated Successfully";
                        TempData["MessageType"] = "Success";
                    }
                    else
                    {
                        TempData["Message"] = "No records updated";
                        TempData["MessageType"] = "Info";
                    }

                    var settings = new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore };
                    HttpContext.Session.SetString("UploadResult", JsonConvert.SerializeObject(result, settings));
                }
                else
                {
                    var sessionData = HttpContext.Session.GetString("UploadResult");
                    if (string.IsNullOrEmpty(sessionData)) return RedirectToAction("Index");

                    result = JsonConvert.DeserializeObject<ValidationResultViewModel>(sessionData);
                }

                // 🔥 PAGINATION
                int skip = (page - 1) * pageSize;

                var pagedResult = new ValidationResultViewModel
                {
                    CleanTitles = result.CleanTitles.Skip(skip).Take(pageSize).ToList(),
                    DuplicateTitlesInExcel = result.DuplicateTitlesInExcel.Skip(skip).Take(pageSize).ToList(),
                    BlockedTitles = new List<TitleValidationViewModel>()
                };

                int maxRows = Math.Max(result.CleanTitles.Count, result.DuplicateTitlesInExcel.Count);

                ViewBag.TotalPages = (int)Math.Ceiling((double)maxRows / pageSize);
                ViewBag.CurrentPage = page;
                ViewBag.PageSize = pageSize;

                ViewData["CanViewTitles"] = HttpContext.Session.GetString("CanViewTitles");
                ViewData["CanDeleteTitles"] = HttpContext.Session.GetString("CanDeleteTitles");
                ViewBag.IsModifiedUpload = true;

                return View("Index", pagedResult);
            }
            catch (Exception ex)
            {
                TempData["Message"] = "Error: " + ex.Message;
                TempData["MessageType"] = "Error";
                return View("Index", new ValidationResultViewModel());
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
                    worksheet.Cell(1, 2).Value = "Paper Id (Required)";
                    worksheet.Cell(1, 3).Value = "Code Ref (Required)";
                    worksheet.Cell(1, 4).Value = "Title (Required)";
                    worksheet.Cell(1, 5).Value = "Financial Year (Required)";
                    worksheet.Cell(1, 6).Value = "Example";

                    // Apply style to header rows
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
                    worksheet.Cell(2, 2).Value = "P1234";
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
                    worksheet.Column(3).Width = 20; // Code Ref
                    worksheet.Column(4).Width = 25; // Title
                    worksheet.Column(5).Width = 23;
                    worksheet.Column(6).Width = 40; // Example column

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

        [HttpGet]
        public IActionResult DownloadTitleTemplate()
        {
            try
            {
                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("ModifiedTitles");

                    // Define the headers
                    worksheet.Cell(1, 1).Value = "Paper Id (Required)";
                    worksheet.Cell(1, 2).Value = "Updated Title (Required)";
                    

                    // Apply style to header rows
                    var headerRange = worksheet.Range("A1:B1");
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
                    worksheet.Cell(2, 1).Value = "P1234";
                    worksheet.Cell(2, 2).Value = "Updated Title";


                    // Apply style to example row
                    var exampleRow = worksheet.Range("A2:E2");
                    exampleRow.Style.Font.FontColor = XLColor.Gray;
                    exampleRow.Style.Font.Italic = true;

                    // Set custom column widths (give space to look neat)
                    worksheet.Column(1).Width = 25; // Invoice No
                    worksheet.Column(2).Width = 30; // Code Ref

                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        var content = stream.ToArray();
                        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "UpdateTitles.xlsx");
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

            ViewData["FilteredCount"] = model.Count;
            return View("ViewTitles", model);
        }

        public async Task<IActionResult> querydata1(string filterId, string PaperId)
        {
            var query = _context.Titles.AsQueryable();

            if (!string.IsNullOrEmpty(filterId) && int.TryParse(filterId, out int id))
            {
                query = query.Where(x => x.Id == id);
            }

            if (!string.IsNullOrEmpty(PaperId))
            {
                query = query.Where(x => x.PaperId.Contains(PaperId));
            }

            var model = await query.ToListAsync();

            ViewData["FilteredCount"] = model.Count;

            // retain values
            ViewData["PaperId"] = PaperId;
            ViewData["FilterId"] = filterId;

            return View("ModifiedTitles", model);
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

        [HttpGet]
        public IActionResult GetDropdownData1()
        {
            var paperId = _context.Titles
     .Where(x => !string.IsNullOrEmpty(x.PaperId) && !string.IsNullOrEmpty(x.UpdatedTitle))
     .Select(x => x.PaperId)
     .Distinct()
     .ToList();

            return Json(new { paperId });
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
                    worksheet.Cells[1, 4].Value = "Paper Id";
                    worksheet.Cells[1, 5].Value = "Title";
                    worksheet.Cells[1, 6].Value = "Created By";
                    worksheet.Cells[1, 7].Value = "Year";
                    worksheet.Cells[1, 8].Value = "Status";

                    int row = 2;
                    foreach (var record in titles)
                    {
                        worksheet.Cells[row, 1].Value = record.Id;
                        worksheet.Cells[row, 2].Value = record.CodeReference;
                        worksheet.Cells[row, 3].Value = record.InvoiceNumber;
                        worksheet.Cells[row, 4].Value = record.PaperId;
                        worksheet.Cells[row, 5].Value = record.Title;
                        worksheet.Cells[row, 6].Value = record.CREATED_BY;
                        worksheet.Cells[row, 7].Value = record.TitleYear;
                        worksheet.Cells[row, 8].Value = record.Status;
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
