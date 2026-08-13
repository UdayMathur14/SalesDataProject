import { CommonModule } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TitleApiService } from './core/services/title-api.service';
import { TitleFilters, TitleImportResult, TitleRecord } from './core/models/title.models';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent implements OnInit {
  readonly titles = signal<TitleRecord[]>([]);
  readonly loading = signal(false);
  readonly message = signal('');
  readonly importResult = signal<TitleImportResult | null>(null);
  readonly selectedIds = signal<Set<number>>(new Set<number>());

  filters: TitleFilters = {};
  selectedFile?: File;
  saveImport = true;

  constructor(public readonly titleApi: TitleApiService) {}

  ngOnInit(): void {
    this.loadTitles();
  }

  loadTitles(): void {
    this.loading.set(true);
    this.titleApi.getTitles(this.filters).subscribe({
      next: (titles) => {
        this.titles.set(titles);
        this.loading.set(false);
      },
      error: () => {
        this.message.set('Titles load nahi ho paaye. Backend API check karein.');
        this.loading.set(false);
      }
    });
  }

  resetFilters(): void {
    this.filters = {};
    this.loadTitles();
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile = input.files?.[0];
  }

  upload(): void {
    if (!this.selectedFile) {
      this.message.set('Upload ke liye Excel file select karein.');
      return;
    }

    this.loading.set(true);
    this.titleApi.importTitles(this.selectedFile, this.saveImport).subscribe({
      next: (result) => {
        this.importResult.set(result);
        this.message.set(result.message);
        this.loading.set(false);
        this.loadTitles();
      },
      error: () => {
        this.message.set('Import fail ho gaya. File format ya API response check karein.');
        this.loading.set(false);
      }
    });
  }

  toggleSelection(id: number, checked: boolean): void {
    const updated = new Set(this.selectedIds());
    checked ? updated.add(id) : updated.delete(id);
    this.selectedIds.set(updated);
  }

  deleteSelected(): void {
    const ids = [...this.selectedIds()];
    if (!ids.length) {
      this.message.set('Delete ke liye at least ek title select karein.');
      return;
    }

    this.loading.set(true);
    this.titleApi.deleteTitles(ids).subscribe({
      next: (response) => {
        this.message.set(response.message);
        this.selectedIds.set(new Set<number>());
        this.loading.set(false);
        this.loadTitles();
      },
      error: () => {
        this.message.set('Delete fail ho gaya. Permission aur backend API check karein.');
        this.loading.set(false);
      }
    });
  }

  trackById(_: number, item: TitleRecord): number {
    return item.id;
  }
}
