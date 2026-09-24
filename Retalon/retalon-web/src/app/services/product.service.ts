import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface ProductInventory {
  quantityAvailable: number;
  quantityReserved: number;
  safetyStockLevel: number;
  procurementLeadTimeDays: number;
  lastUpdated: string;
}

export interface Product {
  productId: number;
  categoryId: number;
  externalProductId?: string;
  name: string;
  barcode?: string;
  description?: string;
  imageUrl?: string;
  price: number;
  currency: string;
  importSource?: string;
  productStatus: string;
  inventory?: ProductInventory;
}

export interface PagedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface BulkImportResult {
  success: boolean;
  importBatchId: number;
  totalRows: number;
  inserted: number;
  updated: number;
  failed: number;
  status: string;
  errors: { rowNumber: number; error: string }[];
}

@Injectable({
  providedIn: 'root'
})
export class ProductService {

  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  search(
    query: string,
    page = 1,
    pageSize = 20,
    sortBy?: string,
    descending = false
  ): Observable<PagedResponse<Product>> {

    let params = new HttpParams()
      .set('query', query)
      .set('page', page)
      .set('pageSize', pageSize)
      .set('descending', descending);

    if (sortBy) {
      params = params.set('sortBy', sortBy);
    }

    return this.http.get<PagedResponse<Product>>(
      `${this.apiUrl}/api/products/search`,
      { params }
    );
  }

  getById(productId: number): Observable<Product> {
    return this.http.get<Product>(
      `${this.apiUrl}/api/products/${productId}`
    );
  }

  getByBarcode(barcode: string): Observable<Product> {
    return this.http.get<Product>(
      `${this.apiUrl}/api/products/barcode/${barcode}`
    );
  }

  importByBarcode(barcode: string): Observable<Product> {
    return this.http.post<Product>(
      `${this.apiUrl}/api/products/import/${barcode}`,
      {}
    );
  }

  importBulk(file: File): Observable<BulkImportResult> {
    const formData = new FormData();
    formData.append('file', file);

    return this.http.post<BulkImportResult>(
      `${this.apiUrl}/api/products/import/bulk`,
      formData
    );
  }
}
