import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface Inventory {
  inventoryId: number;
  productId: number;
  quantityAvailable: number;
  quantityReserved: number;
  safetyStockLevel: number;
  procurementLeadTimeDays: number;
  quantityAfterReservation: number;
  isLowStock: boolean;
  lastUpdated: string;
}

export interface UpdateInventoryRequest {
  quantityAvailable: number;
  quantityReserved: number;
  safetyStockLevel: number;
  procurementLeadTimeDays: number;
}

@Injectable({
  providedIn: 'root'
})
export class InventoryService {

  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  getByProductId(productId: number): Observable<Inventory> {
    return this.http.get<Inventory>(
      `${this.apiUrl}/api/inventory/${productId}`
    );
  }

  update(
    productId: number,
    request: UpdateInventoryRequest
  ): Observable<Inventory> {
    return this.http.put<Inventory>(
      `${this.apiUrl}/api/inventory/${productId}`,
      request
    );
  }

  restock(productId: number, quantity: number): Observable<Inventory> {
    return this.http.post<Inventory>(
      `${this.apiUrl}/api/inventory/${productId}/restock`,
      { quantity }
    );
  }
}
