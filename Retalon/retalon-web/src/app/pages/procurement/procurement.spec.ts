import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting
} from '@angular/common/http/testing';

import { Procurement } from './procurement';
import { AuthService } from '../../services/auth.service';
import { environment } from '../../../environments/environment';

class FakeAuthService {
  staff = false;
  isStaff(): boolean {
    return this.staff;
  }
}

describe('Procurement', () => {
  let component: Procurement;
  let httpMock: HttpTestingController;
  let fakeAuth: FakeAuthService;

  beforeEach(() => {
    fakeAuth = new FakeAuthService();

    TestBed.configureTestingModule({
      imports: [Procurement],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: fakeAuth }
      ]
    });

    const fixture = TestBed.createComponent(Procurement);
    component = fixture.componentInstance;
    fixture.detectChanges();

    httpMock = TestBed.inject(HttpTestingController);

    httpMock.expectOne(`${environment.apiUrl}/api/procurement`).flush([]);
    httpMock.expectOne(`${environment.apiUrl}/api/orders`).flush([]);
  });

  afterEach(() => httpMock.verify());

  it('sends the correct numeric enum value when updating status', () => {
    component.selectedProcurement = {
      procurementId: 5,
      orderId: 1,
      productId: 2,
      requiredQuantity: 3,
      procurementStatus: 'Pending',
      createdDate: '2026-01-01'
    };

    // "Ordered" is index 2 in PROCUREMENT_STATUSES / the backend enum.
    component.newStatus = 'Ordered';
    component.updateStatus();

    const req = httpMock.expectOne(
      `${environment.apiUrl}/api/procurement/5/status`
    );
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toBe(2);

    req.flush({
      procurementId: 5,
      orderId: 1,
      productId: 2,
      requiredQuantity: 3,
      procurementStatus: 'Ordered',
      createdDate: '2026-01-01'
    });

    // updateStatus() reloads the list after a successful save.
    httpMock.expectOne(`${environment.apiUrl}/api/procurement`).flush([]);

    expect(component.selectedProcurement.procurementStatus).toBe('Ordered');
  });

  it('requires an order to be selected before creating a procurement', () => {
    component.createOrderId = null;
    component.createProcurement();

    expect(component.isError).toBe(true);
    httpMock.expectNone(`${environment.apiUrl}/api/procurement`);
  });

  it('reports that nothing was needed when the order is fully in stock', () => {
    // The backend only returns procurement records for items that are
    // short on inventory, so an empty array is a normal, successful result.
    component.createOrderId = 7;
    component.createProcurement();

    const req = httpMock.expectOne(`${environment.apiUrl}/api/procurement`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ orderId: 7 });
    req.flush([]);

    // createProcurement() reloads the list after success.
    httpMock.expectOne(`${environment.apiUrl}/api/procurement`).flush([]);

    expect(component.createOrderId).toBeNull();
    expect(component.isError).toBe(false);
    expect(component.message).toContain('No procurement needed');
  });

  it('reports that records were created when items are short on stock', () => {
    component.createOrderId = 7;
    component.createProcurement();

    const req = httpMock.expectOne(`${environment.apiUrl}/api/procurement`);
    req.flush([
      {
        procurementId: 1,
        orderId: 7,
        productId: 2,
        requiredQuantity: 5,
        procurementStatus: 'Requested',
        createdDate: '2026-01-01'
      }
    ]);

    httpMock.expectOne(`${environment.apiUrl}/api/procurement`).flush([]);

    expect(component.message).toBe('Procurement records created for this order.');
  });
});
