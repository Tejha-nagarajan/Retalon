import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

// Must stay in this exact order: it mirrors the ProcurementStatus
// enum on the backend, since the update-status endpoint expects
// the raw numeric enum value in the request body.
export const PROCUREMENT_STATUSES = [
  'Pending',
  'Requested',
  'Ordered',
  'Received',
  'Cancelled',
  'Completed'
];

export interface Procurement {
  procurementId: number;
  orderId: number;
  productId: number;
  requiredQuantity: number;
  procurementStatus: string;
  expectedArrivalDate?: string;
  createdDate: string;
  updatedDate?: string;
}

@Injectable({
  providedIn: 'root'
})
export class ProcurementService {

  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  createProcurement(orderId: number): Observable<Procurement[]> {
    return this.http.post<Procurement[]>(`${this.apiUrl}/api/procurement`, {
      orderId
    });
  }

  getProcurements(): Observable<Procurement[]> {
    return this.http.get<Procurement[]>(`${this.apiUrl}/api/procurement`);
  }

  getProcurement(procurementId: number): Observable<Procurement> {
    return this.http.get<Procurement>(
      `${this.apiUrl}/api/procurement/${procurementId}`
    );
  }

  updateStatus(
    procurementId: number,
    statusValue: number
  ): Observable<Procurement> {
    return this.http.put<Procurement>(
      `${this.apiUrl}/api/procurement/${procurementId}/status`,
      statusValue
    );
  }
}
