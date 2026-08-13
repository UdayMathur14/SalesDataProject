using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using SalesDataProject.Models;
using SalesDataProject.Models.TitleApi;
using System.Text.RegularExpressions;

namespace SalesDataProject.Controllers.Api
{
    [ApiController]
    [Route("api/titles")]
    public class TitlesApiController : ControllerBase
    {
        private readonly AppDbContext _context;

        public TitlesApiController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<TitleDto>>> GetTitles([FromQuery] TitleQueryParameters queryParameters)
        {
            var query = _context.Titles.AsNoTracking().AsQueryable();

            if (queryParameters.Id.HasValue)
            {
                query = query.Where(title => title.Id == queryParameters.Id.Value);
            }

            if (!string.IsNullOrWhiteSpace(queryParameters.CodeReference))
            {
                query = query.Where(title => title.CodeReference != null && title.CodeReference.Contains(queryParameters.CodeReference));
            }

            if (!string.IsNullOrWhiteSpace(queryParameters.InvoiceNumber))
            {
                query = query.Where(title => title.InvoiceNumber != null && title.InvoiceNumber.Contains(queryParameters.InvoiceNumber));
            }

            if (!string.IsNullOrWhiteSpace(queryParameters.Title))
            {
                query = query.Where(title => title.Title != null && title.Title.Contains(queryParameters.Title));
            }

            if (!string.IsNullOrWhiteSpace(queryParameters.TitleYear))
            {
                query = query.Where(title => title.TitleYear != null && title.TitleYear.Contains(queryParameters.TitleYear));
            }

            var titles = await query
                .OrderByDescending(title => title.Id)
                .Select(title => ToDto(title))
                .ToListAsync();

            return Ok(titles);
        }

        [HttpGet("dropdowns")]
        public async Task<ActionResult<TitleDropdownsDto>> GetDropdowns()
        {
            var codeReferences = await _context.Titles.AsNoTracking()
                .Where(title => !string.IsNullOrEmpty(title.CodeReference))
                .Select(title => title.CodeReference!)
                .Distinct()
                .OrderBy(value => value)
                .ToListAsync();

            var invoiceNumbers = await _context.Titles.AsNoTracking()
                .Where(title => !string.IsNullOrEmpty(title.InvoiceNumber))
                .Select(title => title.InvoiceNumber!)
                .Distinct()
                .OrderBy(value => value)
                .ToListAsync();

            var titles = await _context.Titles.AsNoTracking()
                .Where(title => !string.IsNullOrEmpty(title.Title))
                .Select(title => title.Title!)
                .Distinct()
                .OrderBy(value => value)
                .ToListAsync();

            return Ok(new TitleDropdownsDto
            {
                CodeReferences = codeReferences,
                InvoiceNumbers = invoiceNumbers,
                Titles = titles
            });
        }

        [HttpPost("imports")]
        [RequestSizeLimit(200 * 1024 * 1024)]
        public async Task<ActionResult<TitleImportResultDto>> ImportTitles(IFormFile file, [FromQuery] bool save = true)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "Please upload a title Excel file." });
            }

            var username = HttpContext.Session.GetString("Username") ?? "api-user";
            var result = await ValidateTitleWorkbook(file, username);

            if (save && result.CleanTitles.Any())
            {
                var records = result.CleanTitles.Select(row => new TitleValidationViewModel
                {
                    Title = row.Title,
                    InvoiceNumber = row.InvoiceNumber,
                    CodeReference = row.CodeReference,
                    CREATED_ON = DateOnly.FromDateTime(DateTime.Now),
                    CREATED_BY = username,
                    ReferenceTitle = CleanTitle(row.Title),
                    Status = "Clean",
                    TitleYear = row.TitleYear
                }).ToList();

                _context.Titles.AddRange(records);
                await _context.SaveChangesAsync();

                result.Saved = true;
                result.Message = $"{records.Count} clean title record(s) saved successfully.";
            }
            else
            {
                result.Saved = false;
                result.Message = save ? "No clean title records were available to save." : "Validation completed. No data was saved.";
            }

            return CreatedAtAction(nameof(GetTitles), null, result);
        }

        [HttpDelete]
        public async Task<ActionResult<DeleteTitlesResponse>> DeleteTitles([FromBody] DeleteTitlesRequest request)
        {
            if (request.Ids == null || !request.Ids.Any())
            {
                return BadRequest(new { message = "Select at least one title to delete." });
            }

            var titles = await _context.Titles.Where(title => request.Ids.Contains(title.Id)).ToListAsync();
            _context.Titles.RemoveRange(titles);
            await _context.SaveChangesAsync();

            return Ok(new DeleteTitlesResponse
            {
                DeletedCount = titles.Count,
                Message = $"{titles.Count} title record(s) deleted successfully."
            });
        }

        [HttpGet("template")]
        public IActionResult DownloadTemplate()
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("UploadTitles");

            worksheet.Cell(1, 1).Value = "Invoice No (Required)";
            worksheet.Cell(1, 2).Value = "Code Ref (Required)";
            worksheet.Cell(1, 3).Value = "Title (Required)";
            worksheet.Cell(1, 4).Value = "Financial Year (Required)";
            worksheet.Cell(1, 5).Value = "Example";
            worksheet.Cell(2, 1).Value = "INV123";
            worksheet.Cell(2, 2).Value = "CR456";
            worksheet.Cell(2, 3).Value = "Sample Title";
            worksheet.Cell(2, 4).Value = "2025-26";
            worksheet.Cell(2, 5).Value = "Example row. Please delete and follow this format.";

            worksheet.Range("A1:E1").Style.Font.Bold = true;
            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "UploadTitles.xlsx");
        }

        [HttpGet("export")]
        public async Task<IActionResult> ExportTitles()
        {
            var titles = await _context.Titles.AsNoTracking().OrderByDescending(title => title.Id).ToListAsync();
            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Titles");
            var headers = new[] { "Id", "Code Ref", "Invoice No", "Title", "Created By", "Year", "Status" };

            for (var column = 0; column < headers.Length; column++)
            {
                worksheet.Cells[1, column + 1].Value = headers[column];
            }

            for (var index = 0; index < titles.Count; index++)
            {
                var row = index + 2;
                worksheet.Cells[row, 1].Value = titles[index].Id;
                worksheet.Cells[row, 2].Value = titles[index].CodeReference;
                worksheet.Cells[row, 3].Value = titles[index].InvoiceNumber;
                worksheet.Cells[row, 4].Value = titles[index].Title;
                worksheet.Cells[row, 5].Value = titles[index].CREATED_BY;
                worksheet.Cells[row, 6].Value = titles[index].TitleYear;
                worksheet.Cells[row, 7].Value = titles[index].Status;
            }

            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
            var stream = new MemoryStream();
            package.SaveAs(stream);
            stream.Position = 0;

            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"TitleRecords-{DateTime.Now:yyyyMMddHHmmss}.xlsx");
        }

        private async Task<TitleImportResultDto> ValidateTitleWorkbook(IFormFile file, string username)
        {
            var cleanTitles = new List<TitleImportRowDto>();
            var blockedTitles = new List<TitleImportRowDto>();
            var invalidTitles = new List<TitleImportRowDto>();
            var titlesInExcel = new HashSet<string>();
            var existingTitles = await _context.Titles.AsNoTracking().ToListAsync();

            using var package = new ExcelPackage(file.OpenReadStream());
            var worksheet = package.Workbook.Worksheets.FirstOrDefault();
            if (worksheet?.Dimension == null)
            {
                return new TitleImportResultDto { Message = "The uploaded workbook does not contain title rows." };
            }

            for (var row = 2; row <= worksheet.Dimension.Rows; row++)
            {
                var invoiceNumber = worksheet.Cells[row, 1].Text;
                var codeReference = worksheet.Cells[row, 2].Text;
                var title = worksheet.Cells[row, 3].Text;
                var titleYear = worksheet.Cells[row, 4].Text;

                if (string.IsNullOrWhiteSpace(title))
                {
                    break;
                }

                var referenceTitle = CleanTitle(title);
                var validationError = GetValidationError(invoiceNumber, codeReference, titleYear, referenceTitle, titlesInExcel, existingTitles);
                if (!string.IsNullOrEmpty(validationError))
                {
                    invalidTitles.Add(ToImportRow(row, title, invoiceNumber, codeReference, titleYear, validationError));
                    continue;
                }

                titlesInExcel.Add(referenceTitle);
                var existingTitle = existingTitles.FirstOrDefault(storedTitle => storedTitle.ReferenceTitle == referenceTitle);
                var importRow = ToImportRow(row, title, invoiceNumber, codeReference, titleYear, existingTitle == null ? "Clean" : "Blocked", existingTitle);

                if (existingTitle == null)
                {
                    cleanTitles.Add(importRow);
                }
                else
                {
                    blockedTitles.Add(importRow);
                }
            }

            return new TitleImportResultDto
            {
                CleanTitles = cleanTitles,
                BlockedTitles = blockedTitles,
                DuplicateTitlesInExcel = invalidTitles,
                Message = "Validation completed."
            };
        }

        private static string? GetValidationError(string invoiceNumber, string codeReference, string titleYear, string referenceTitle, HashSet<string> titlesInExcel, List<TitleValidationViewModel> existingTitles)
        {
            if (string.IsNullOrWhiteSpace(titleYear)) return "Year Missing";
            if (string.IsNullOrWhiteSpace(invoiceNumber)) return "Invoice No is Missing";
            if (string.IsNullOrWhiteSpace(codeReference)) return "Code Reference No is Missing";
            if (!IsValidFinancialYear(titleYear)) return "Invalid Financial Year";
            if (titlesInExcel.Contains(referenceTitle)) return "Duplicate in Excel";

            var invoiceExists = existingTitles.Any(title => title.InvoiceNumber == invoiceNumber && title.CodeReference == codeReference && title.TitleYear == titleYear);
            return invoiceExists ? "Invoice with codeRef already exists" : null;
        }

        private static bool IsValidFinancialYear(string year)
        {
            var yearParts = year.Split('-');
            if (yearParts.Length != 2) return false;
            if (!int.TryParse(yearParts[0], out var startYear) || !int.TryParse(yearParts[1], out var endYearPart)) return false;
            if (startYear < 1999 || startYear > 2099) return false;
            return endYearPart == (startYear + 1) % 100;
        }

        private static string CleanTitle(string? title)
        {
            if (string.IsNullOrWhiteSpace(title)) return string.Empty;
            return Regex.Replace(title, @"[^a-zA-Z0-9]", string.Empty).ToLower();
        }

        private static TitleDto ToDto(TitleValidationViewModel title) => new()
        {
            Id = title.Id,
            RowNumber = title.RowNumber,
            CodeReference = title.CodeReference,
            InvoiceNumber = title.InvoiceNumber,
            Title = title.Title,
            CreatedBy = title.CREATED_BY,
            CreatedOn = title.CREATED_ON.ToString("yyyy-MM-dd"),
            Status = title.Status,
            ReferenceTitle = title.ReferenceTitle,
            TitleYear = title.TitleYear
        };

        private static TitleImportRowDto ToImportRow(int row, string title, string invoiceNumber, string codeReference, string titleYear, string status, TitleValidationViewModel? blockedTitle = null) => new()
        {
            RowNumber = row,
            Title = title,
            InvoiceNumber = invoiceNumber,
            CodeReference = codeReference,
            Status = status,
            TitleYear = titleYear,
            BlockedId = blockedTitle?.Id,
            BlockedByInvoiceNo = blockedTitle?.InvoiceNumber,
            BlockedCodeRef = blockedTitle?.CodeReference
        };
    }
}
