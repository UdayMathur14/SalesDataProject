export interface TitleRecord {
  id: number;
  rowNumber: number;
  codeReference?: string;
  invoiceNumber?: string;
  title?: string;
  createdBy?: string;
  createdOn?: string;
  status?: string;
  referenceTitle?: string;
  titleYear?: string;
}

export interface TitleFilters {
  id?: number | null;
  codeReference?: string;
  invoiceNumber?: string;
  title?: string;
  titleYear?: string;
}

export interface TitleImportRow {
  rowNumber: number;
  codeReference?: string;
  invoiceNumber?: string;
  title?: string;
  status?: string;
  titleYear?: string;
  blockedId?: number;
  blockedByInvoiceNo?: string;
  blockedCodeRef?: string;
}

export interface TitleImportResult {
  saved: boolean;
  message: string;
  cleanTitles: TitleImportRow[];
  blockedTitles: TitleImportRow[];
  duplicateTitlesInExcel: TitleImportRow[];
}

export interface TitleDropdowns {
  codeReferences: string[];
  invoiceNumbers: string[];
  titles: string[];
}
