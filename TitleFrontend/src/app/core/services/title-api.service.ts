import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { TitleDropdowns, TitleFilters, TitleImportResult, TitleRecord } from '../models/title.models';

@Injectable({ providedIn: 'root' })
export class TitleApiService {
  private readonly baseUrl = '/api/titles';

  constructor(private readonly http: HttpClient) {}

  getTitles(filters: TitleFilters): Observable<TitleRecord[]> {
    let params = new HttpParams();
    Object.entries(filters).forEach(([key, value]) => {
      if (value !== undefined && value !== null && `${value}`.trim() !== '') {
        params = params.set(key, `${value}`);
      }
    });
    return this.http.get<TitleRecord[]>(this.baseUrl, { params });
  }

  getDropdowns(): Observable<TitleDropdowns> {
    return this.http.get<TitleDropdowns>(`${this.baseUrl}/dropdowns`);
  }

  importTitles(file: File, save: boolean): Observable<TitleImportResult> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<TitleImportResult>(`${this.baseUrl}/imports`, formData, {
      params: new HttpParams().set('save', save)
    });
  }

  deleteTitles(ids: number[]): Observable<{ deletedCount: number; message: string }> {
    return this.http.delete<{ deletedCount: number; message: string }>(this.baseUrl, { body: { ids } });
  }

  downloadTemplate(): void {
    window.open(`${this.baseUrl}/template`, '_blank');
  }

  exportTitles(): void {
    window.open(`${this.baseUrl}/export`, '_blank');
  }
}
