import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting
} from '@angular/common/http/testing';

import {
  PROCUREMENT_STATUSES,
  ProcurementService
} from './procurement.service';
import { environment } from '../../environments/environment';

describe('PROCUREMENT_STATUSES', () => {
  // This order must match the backend's ProcurementStatus enum exactly,
  // since the update-status endpoint is sent the raw numeric enum value.
  it('matches the backend ProcurementStatus enum order', () => {
    expect(PROCUREMENT_STATUSES).toEqual([
      'Pending',
      'Requested',
      'Ordered',
      'Received',
      'Cancelled',
      'Completed'
    ]);
  });
});

describe('ProcurementService', () => {
  let service: ProcurementService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(ProcurementService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('createProcurement posts the order id', () => {
    service.createProcurement(4).subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/api/procurement`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ orderId: 4 });

    req.flush([]);
  });

  it('getProcurements sends a GET to /api/procurement', () => {
    service.getProcurements().subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/api/procurement`);
    expect(req.request.method).toBe('GET');

    req.flush([]);
  });

  it('getProcurement sends a GET to /api/procurement/{id}', () => {
    service.getProcurement(3).subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/api/procurement/3`);
    expect(req.request.method).toBe('GET');

    req.flush({});
  });

  it('updateStatus PUTs the raw numeric enum value, not an object', () => {
    service.updateStatus(3, 2).subscribe();

    const req = httpMock.expectOne(
      `${environment.apiUrl}/api/procurement/3/status`
    );

    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toBe(2);

    req.flush({});
  });
});
