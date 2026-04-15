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

            try
            {
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

                if (file != null && file.Length > 0)
                {
                    result = new ValidationResultViewModel();

                    var allTitles = await _context.Titles.ToListAsync();

                    // 🔥 FAST LOOKUPS
                    var titleSet = new HashSet<string>(
                        allTitles.Select(t =>
                            CleanTitle(!string.IsNullOrWhiteSpace(t.UpdatedReferenceTitle)
                                ? t.UpdatedReferenceTitle
                                : t.ReferenceTitle))
                    );

                    var paperIdSet = new HashSet<string>(
                        allTitles.Select(t => t.PaperId)
                    );

                    var titlesInExcel = new HashSet<string>();
                    var paperIdInExcel = new HashSet<string>();

                    using (var package = new ExcelPackage(file.OpenReadStream()))
                    {
                        var worksheet = package.Workbook.Worksheets[0];
                        var rowCount = worksheet.Dimension?.Rows ?? 0;

                        for (int row = 2; row <= rowCount; row++)
                        {
                            var invoiceNumber = worksheet.Cells[row, 1].Text?.Trim();
                            var paperId = worksheet.Cells[row, 2].Text?.Trim();
                            var codeReference = worksheet.Cells[row, 3].Text?.Trim();
                            var title = worksheet.Cells[row, 4].Text?.Trim();
                            var yearTitle = worksheet.Cells[row, 5].Text?.Trim();

                            if (string.IsNullOrWhiteSpace(title))
                                continue;

                            string cleanTitle = CleanTitle(title);

                            var tv = new TitleValidationViewModel
                            {
                                RowNumber = row,
                                Title = title,
                                InvoiceNumber = invoiceNumber,
                                PaperId = paperId,
                                CodeReference = codeReference,
                                TitleYear = yearTitle,
                                CREATED_ON = DateOnly.FromDateTime(DateTime.Now),
                                CREATED_BY = username,
                                ReferenceTitle = cleanTitle
                            };

                            // 🔴 VALIDATIONS

                            if (string.IsNullOrWhiteSpace(yearTitle))
                            {
                                tv.Status = "Year Missing";
                                result.DuplicateTitlesInExcel.Add(tv);
                                continue;
                            }

                            if (!IsValidFinancialYear(yearTitle))
                            {
                                tv.Status = "Invalid Financial Year";
                                result.DuplicateTitlesInExcel.Add(tv);
                                continue;
                            }

                            if (string.IsNullOrWhiteSpace(paperId))
                            {
                                tv.Status = "PaperId Missing";
                                result.DuplicateTitlesInExcel.Add(tv);
                                continue;
                            }

                            if (string.IsNullOrWhiteSpace(invoiceNumber))
                            {
                                tv.Status = "Invoice Number Missing";
                                result.DuplicateTitlesInExcel.Add(tv);
                                continue;
                            }

                            if (string.IsNullOrWhiteSpace(codeReference))
                            {
                                tv.Status = "Code Reference Missing";
                                result.DuplicateTitlesInExcel.Add(tv);
                                continue;
                            }


                            // 🔴 PAPER ID DUPLICATE (DB + Excel)
                            if (paperIdSet.Contains(paperId))
                            {
                                tv.Status = "PaperId already exists in DB";
                                result.DuplicateTitlesInExcel.Add(tv);
                                continue;
                            }

                            // 🔴 TITLE DUPLICATE IN EXCEL
                            if (titlesInExcel.Contains(cleanTitle))
                            {
                                tv.Status = "Duplicate title in Excel";
                                result.DuplicateTitlesInExcel.Add(tv);
                                continue;
                            }
                            if (paperIdInExcel.Contains(paperId))
                            {
                                tv.Status = "Duplicate PaperId in Excel";
                                result.DuplicateTitlesInExcel.Add(tv);
                                continue;
                            }

                            // 🔴 TITLE DUPLICATE IN DB
                            if (titleSet.Contains(cleanTitle))
                            {
                                tv.Status = "Duplicate title in DB";
                                result.BlockedTitles.Add(tv);
                                continue;
                            }

                            // ✅ CLEAN
                            tv.Status = "Clean";
                            result.CleanTitles.Add(tv);

                            // 🔥 UPDATE RUNTIME SETS
                            titlesInExcel.Add(cleanTitle);
                            titleSet.Add(cleanTitle);
                            paperIdSet.Add(paperId);
                        }
                    }

                    // ✅ SAVE
                    if (!testMode && result.CleanTitles.Any())
                    {
                        var entities = result.CleanTitles.Select(tv => new TitleValidationViewModel
                        {
                            Title = tv.Title,
                            InvoiceNumber = tv.InvoiceNumber,
                            PaperId = tv.PaperId,
                            CodeReference = tv.CodeReference,
                            CREATED_ON = DateOnly.FromDateTime(DateTime.Now),
                            CREATED_BY = username,
                            ReferenceTitle = tv.ReferenceTitle,
                            Status = "Clean",
                            TitleYear = tv.TitleYear
                        }).ToList();

                        _context.Titles.AddRange(entities);
                        await _context.SaveChangesAsync();

                        TempData["Message"] = $"Total: {result.CleanTitles.Count + result.DuplicateTitlesInExcel.Count + result.BlockedTitles.Count}, Saved: {result.CleanTitles.Count}, Failed: {result.DuplicateTitlesInExcel.Count + result.BlockedTitles.Count}";
                        TempData["MessageType"] = "Success";
                    }
                    else
                    {
                        TempData["Message"] = testMode ? "Test Mode: Validation Completed" : "Upload Completed";
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

                // PAGINATION
                int skip = (page - 1) * pageSize;

                var pagedResult = new ValidationResultViewModel
                {
                    CleanTitles = result.CleanTitles.Skip(skip).Take(pageSize).ToList(),
                    BlockedTitles = result.BlockedTitles.Skip(skip).Take(pageSize).ToList(),
                    DuplicateTitlesInExcel = result.DuplicateTitlesInExcel.Skip(skip).Take(pageSize).ToList()
                };

                int maxRows = Math.Max(result.CleanTitles.Count,
                    Math.Max(result.BlockedTitles.Count, result.DuplicateTitlesInExcel.Count));

                ViewBag.TotalPages = (int)Math.Ceiling((double)maxRows / pageSize);
                ViewBag.CurrentPage = page;
                ViewBag.PageSize = pageSize;

                ViewData["CanViewTitles"] = HttpContext.Session.GetString("CanViewTitles");
                ViewData["CanDeleteTitles"] = HttpContext.Session.GetString("CanDeleteTitles");

                return View("Index", pagedResult);
            }
            catch (Exception ex)
            {
                TempData["Message"] = "Error: " + ex.Message;
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

                    // DB lookup by PaperId
                    var titleDict = allTitles.ToDictionary(t => t.PaperId);

                    // Existing title set from DB
                    var titleSet = new HashSet<string>(
                        allTitles.Select(t =>
                            CleanTitle(!string.IsNullOrWhiteSpace(t.UpdatedReferenceTitle)
                                ? t.UpdatedReferenceTitle
                                : t.ReferenceTitle))
                    );

                    // Track duplicate PaperId inside uploaded Excel
                    var uploadedPaperIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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

                            // 1. Duplicate PaperId in uploaded Excel
                            if (!uploadedPaperIds.Add(paperId))
                            {
                                result.DuplicateTitlesInExcel.Add(new TitleValidationViewModel
                                {
                                    RowNumber = row,
                                    PaperId = paperId,
                                    UpdatedTitle = updatedTitle,
                                    Status = "Duplicate PaperId in Excel"
                                });
                                continue;
                            }

                            // 2. PaperId must exist in DB
                            if (!titleDict.TryGetValue(paperId, out var existingRecord))
                            {
                                result.DuplicateTitlesInExcel.Add(new TitleValidationViewModel
                                {
                                    RowNumber = row,
                                    PaperId = paperId,
                                    UpdatedTitle = updatedTitle,
                                    Status = "PaperId not found"
                                });
                                continue;
                            }

                            // 3. Duplicate title check (DB + runtime)
                            if (titleSet.Contains(cleanTitle))
                            {
                                result.DuplicateTitlesInExcel.Add(new TitleValidationViewModel
                                {
                                    RowNumber = row,
                                    PaperId = paperId,
                                    UpdatedTitle = updatedTitle,
                                    Status = "Duplicate Title"
                                });
                                continue;
                            }

                            // Update
                            existingRecord.UpdatedTitle = updatedTitle;
                            existingRecord.UpdatedReferenceTitle = cleanTitle;
                            existingRecord.UpdatedTitleBy = username;

                            // Prevent duplicate title in same upload
                            titleSet.Add(cleanTitle);

                            result.CleanTitles.Add(new TitleValidationViewModel
                            {
                                RowNumber = row,
                                PaperId = paperId,
                                UpdatedTitle = updatedTitle,
                                UpdatedTitleBy = username,
                                Status = "PASS"
                            });
                        }
                    }

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

                    var settings = new JsonSerializerSettings
                    {
                        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                    };

                    HttpContext.Session.SetString("UploadResult", JsonConvert.SerializeObject(result, settings));
                }
                else
                {
                    var sessionData = HttpContext.Session.GetString("UploadResult");
                    if (string.IsNullOrEmpty(sessionData))
                        return RedirectToAction("Index");

                    result = JsonConvert.DeserializeObject<ValidationResultViewModel>(sessionData);
                }

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
                    worksheet.Cell(1, 1).Value = "Lot Number(Required)";
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
                    worksheet.Cells[1, 3].Value = "Lot No";
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
