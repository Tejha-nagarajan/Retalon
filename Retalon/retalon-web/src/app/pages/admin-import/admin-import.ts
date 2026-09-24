import { ChangeDetectorRef, Component } from '@angular/core';
import { CommonModule } from '@angular/common';

import { BulkImportResult, ProductService } from '../../services/product.service';
import { getErrorMessage } from '../../shared/error-message';

@Component({
  selector: 'app-admin-import',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './admin-import.html'
})
export class AdminImport {

  selectedFile: File | null = null;
  result: BulkImportResult | null = null;

  message = '';
  isError = false;
  isUploading = false;

  constructor(
    private productService: ProductService,
    private cdr: ChangeDetectorRef
  ) {}

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile = input.files && input.files.length > 0
      ? input.files[0]
      : null;
  }

  upload(): void {
    this.message = '';
    this.isError = false;
    this.result = null;

    if (!this.selectedFile) {
      this.message = 'Choose an Excel file first.';
      this.isError = true;
      return;
    }

    this.isUploading = true;

    this.productService.importBulk(this.selectedFile).subscribe({
      next: result => {
        this.isUploading = false;
        this.result = result;
        this.message = `Import finished: ${result.inserted} inserted, ${result.updated} updated, ${result.failed} failed.`;
        this.cdr.markForCheck();
      },
      error: error => {
        this.isUploading = false;
        this.isError = true;
        this.message = getErrorMessage(error, 'Unable to import the file.');
        this.cdr.markForCheck();
      }
    });
  }
}
