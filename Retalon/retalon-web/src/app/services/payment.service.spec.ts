import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting
} from '@angular/common/http/testing';

import { PaymentService } from './payment.service';
import { environment } from '../../environments/environment';

describe('PaymentService', () => {
  let service: PaymentService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(PaymentService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('createPayment posts the order id to /api/payments/create', () => {
    service.createPayment(12).subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/api/payments/create`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ orderId: 12 });

    req.flush({
      paymentId: 1,
      orderId: 12,
      stripePaymentIntentId: 'pi_1',
      amount: 10,
      currency: 'USD',
      paymentStatus: 'Pending',
      createdDate: '2026-01-01'
    });
  });

  it('confirmTestPayment defaults the test payment method to pm_card_visa', () => {
    service.confirmTestPayment(1).subscribe();

    const req = httpMock.expectOne(
      `${environment.apiUrl}/api/payments/confirm-test`
    );
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      paymentId: 1,
      testPaymentMethod: 'pm_card_visa'
    });

    req.flush({
      paymentId: 1,
      orderId: 12,
      stripePaymentIntentId: 'pi_1',
      amount: 10,
      currency: 'USD',
      paymentStatus: 'Succeeded',
      createdDate: '2026-01-01'
    });
  });

  it('confirmTestPayment accepts a custom test payment method', () => {
    service.confirmTestPayment(1, 'pm_card_visa_declined').subscribe();

    const req = httpMock.expectOne(
      `${environment.apiUrl}/api/payments/confirm-test`
    );
    expect(req.request.body.testPaymentMethod).toBe('pm_card_visa_declined');

    req.flush({});
  });
});
