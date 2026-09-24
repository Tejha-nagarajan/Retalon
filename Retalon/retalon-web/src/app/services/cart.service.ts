import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface CartItem {
  cartItemId: number;
  productId: number;
  productName: string;
  imageUrl?: string;
  price: number;
  quantity: number;
  subtotal: number;
}

export interface Cart {
  cartId: string;
  items: CartItem[];
  total: number;
}

@Injectable({
  providedIn: 'root'
})
export class CartService {

  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  getCart(): Observable<Cart> {
    return this.http.get<Cart>(`${this.apiUrl}/api/cart`);
  }

  addItem(productId: number, quantity: number): Observable<Cart> {
    return this.http.post<Cart>(`${this.apiUrl}/api/cart/items`, {
      productId,
      quantity
    });
  }

  removeItem(cartItemId: number): Observable<void> {
    return this.http.delete<void>(
      `${this.apiUrl}/api/cart/items/${cartItemId}`
    );
  }
}
