import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface OrderItem {
  orderItemId: number;
  productId: number;
  productName: string;
  quantity: number;
  unitPrice: number;
  subtotal: number;
  deliveryDays: number;
}

export interface Order {
  orderId: number;
  userId: string;
  orderStatus: string;
  totalAmount: number;
  expectedDeliveryDate: string;
  createdDate: string;
  items: OrderItem[];
}

export interface CreateOrderResult {
  orderId: number;
  orderStatus: string;
  totalAmount: number;
  expectedDeliveryDate: string;
}

@Injectable({
  providedIn: 'root'
})
export class OrderService {

  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  createOrder(): Observable<CreateOrderResult> {
    return this.http.post<CreateOrderResult>(`${this.apiUrl}/api/orders`, {});
  }

  getOrders(): Observable<Order[]> {
    return this.http.get<Order[]>(`${this.apiUrl}/api/orders`);
  }

  getOrder(orderId: number): Observable<Order> {
    return this.http.get<Order>(`${this.apiUrl}/api/orders/${orderId}`);
  }
}
