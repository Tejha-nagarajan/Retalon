import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting
} from '@angular/common/http/testing';

import { InventoryService } from './inventory.service';
import { environment } from '../../environments/environment';

describe('InventoryService', () => {
  let service: InventoryService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(InventoryService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('getByProductId sends a GET to /api/inventory/{id}', () => {
    service.getByProductId(9).subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/api/inventory/9`);
    expect(req.request.method).toBe('GET');

    req.flush({});
  });

  it('update PUTs the full inventory payload', () => {
    service
      .update(9, {
        quantityAvailable: 100,
        quantityReserved: 5,
        safetyStockLevel: 10,
        procurementLeadTimeDays: 3
      })
      .subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/api/inventory/9`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({
      quantityAvailable: 100,
      quantityReserved: 5,
      safetyStockLevel: 10,
      procurementLeadTimeDays: 3
    });

    req.flush({});
  });

  it('restock posts the quantity to /api/inventory/{id}/restock', () => {
    service.restock(9, 20).subscribe();

    const req = httpMock.expectOne(
      `${environment.apiUrl}/api/inventory/9/restock`
    );
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ quantity: 20 });

    req.flush({});
  });
});
