import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting
} from '@angular/common/http/testing';

import { OrderService } from './order.service';
import { environment } from '../../environments/environment';

describe('OrderService', () => {
  let service: OrderService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(OrderService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('createOrder posts to /api/orders with an empty body', () => {
    service.createOrder().subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/api/orders`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({});

    req.flush({
      orderId: 1,
      orderStatus: 'Pending',
      totalAmount: 10,
      expectedDeliveryDate: '2026-01-01'
    });
  });

  it('getOrders sends a GET to /api/orders', () => {
    service.getOrders().subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/api/orders`);
    expect(req.request.method).toBe('GET');

    req.flush([]);
  });

  it('getOrder sends a GET to /api/orders/{id}', () => {
    service.getOrder(5).subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/api/orders/5`);
    expect(req.request.method).toBe('GET');

    req.flush({
      orderId: 5,
      userId: 'u1',
      orderStatus: 'Pending',
      totalAmount: 10,
      expectedDeliveryDate: '2026-01-01',
      createdDate: '2026-01-01',
      items: []
    });
  });
});
