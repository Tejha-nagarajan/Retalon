import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface Payment {
  paymentId: number;
  orderId: number;
  stripePaymentIntentId: string;
  amount: number;
  currency: string;
  paymentStatus: string;
  clientSecret?: string;
  failureReason?: string;
  createdDate: string;
  updatedDate?: string;
}

@Injectable({
  providedIn: 'root'
})
export class PaymentService {

  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  createPayment(orderId: number): Observable<Payment> {
    return this.http.post<Payment>(`${this.apiUrl}/api/payments/create`, {
      orderId
    });
  }

  confirmTestPayment(
    paymentId: number,
    testPaymentMethod = 'pm_card_visa'
  ): Observable<Payment> {
    return this.http.post<Payment>(
      `${this.apiUrl}/api/payments/confirm-test`,
      { paymentId, testPaymentMethod }
    );
  }
}
