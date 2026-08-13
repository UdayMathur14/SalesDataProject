namespace SalesDataProject.Models.TitleApi
{
    public class TitleDto
    {
        public int Id { get; set; }
        public int RowNumber { get; set; }
        public string? CodeReference { get; set; }
        public string? InvoiceNumber { get; set; }
        public string? Title { get; set; }
        public string? CreatedBy { get; set; }
        public string? CreatedOn { get; set; }
        public string? Status { get; set; }
        public string? ReferenceTitle { get; set; }
        public string? TitleYear { get; set; }
    }

    public class TitleQueryParameters
    {
        public int? Id { get; set; }
        public string? CodeReference { get; set; }
        public string? InvoiceNumber { get; set; }
        public string? Title { get; set; }
        public string? TitleYear { get; set; }
    }

    public class TitleImportRowDto
    {
        public int RowNumber { get; set; }
        public string? CodeReference { get; set; }
        public string? InvoiceNumber { get; set; }
        public string? Title { get; set; }
        public string? Status { get; set; }
        public string? TitleYear { get; set; }
        public int? BlockedId { get; set; }
        public string? BlockedByInvoiceNo { get; set; }
        public string? BlockedCodeRef { get; set; }
    }

    public class TitleImportResultDto
    {
        public bool Saved { get; set; }
        public string Message { get; set; } = string.Empty;
        public IReadOnlyList<TitleImportRowDto> CleanTitles { get; set; } = Array.Empty<TitleImportRowDto>();
        public IReadOnlyList<TitleImportRowDto> BlockedTitles { get; set; } = Array.Empty<TitleImportRowDto>();
        public IReadOnlyList<TitleImportRowDto> DuplicateTitlesInExcel { get; set; } = Array.Empty<TitleImportRowDto>();
    }

    public class TitleDropdownsDto
    {
        public IReadOnlyList<string> CodeReferences { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> InvoiceNumbers { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> Titles { get; set; } = Array.Empty<string>();
    }

    public class DeleteTitlesRequest
    {
        public IReadOnlyList<int> Ids { get; set; } = Array.Empty<int>();
    }

    public class DeleteTitlesResponse
    {
        public int DeletedCount { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
