import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting
} from '@angular/common/http/testing';

import { ProductService } from './product.service';
import { environment } from '../../environments/environment';

describe('ProductService', () => {
  let service: ProductService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(ProductService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('search always sends query, page, pageSize and descending', () => {
    service.search('milk', 2, 15).subscribe();

    const req = httpMock.expectOne(
      r => r.url === `${environment.apiUrl}/api/products/search`
    );

    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('query')).toBe('milk');
    expect(req.request.params.get('page')).toBe('2');
    expect(req.request.params.get('pageSize')).toBe('15');
    expect(req.request.params.get('descending')).toBe('false');
    expect(req.request.params.has('sortBy')).toBe(false);

    req.flush({ items: [], page: 2, pageSize: 15, totalCount: 0, totalPages: 0 });
  });

  it('search includes sortBy only when provided', () => {
    service.search('milk', 1, 20, 'price', true).subscribe();

    const req = httpMock.expectOne(
      r => r.url === `${environment.apiUrl}/api/products/search`
    );

    expect(req.request.params.get('sortBy')).toBe('price');
    expect(req.request.params.get('descending')).toBe('true');

    req.flush({ items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 });
  });

  it('getById requests the product by id', () => {
    service.getById(42).subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/api/products/42`);
    expect(req.request.method).toBe('GET');
    req.flush({});
  });

  it('getByBarcode requests the product by barcode', () => {
    service.getByBarcode('012345').subscribe();

    const req = httpMock.expectOne(
      `${environment.apiUrl}/api/products/barcode/012345`
    );
    expect(req.request.method).toBe('GET');
    req.flush({});
  });

  it('importByBarcode posts to the import endpoint', () => {
    service.importByBarcode('012345').subscribe();

    const req = httpMock.expectOne(
      `${environment.apiUrl}/api/products/import/012345`
    );
    expect(req.request.method).toBe('POST');
    req.flush({});
  });

  it('importBulk sends the file as multipart form data under the "file" field', () => {
    const file = new File(['a,b,c'], 'products.xlsx');

    service.importBulk(file).subscribe();

    const req = httpMock.expectOne(
      `${environment.apiUrl}/api/products/import/bulk`
    );

    expect(req.request.method).toBe('POST');
    expect(req.request.body instanceof FormData).toBe(true);
    expect((req.request.body as FormData).get('file')).toBe(file);

    req.flush({
      success: true,
      importBatchId: 1,
      totalRows: 1,
      inserted: 1,
      updated: 0,
      failed: 0,
      status: 'Completed',
      errors: []
    });
  });
});
