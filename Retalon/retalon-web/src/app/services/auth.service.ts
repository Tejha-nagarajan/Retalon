import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';

export interface RegisterRequest {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
  phoneNumber?: string;
  addressLine1: string;
  addressLine2?: string;
  city: string;
  state?: string;
  postalCode: string;
  country: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
}

// This is what System.Security.Claims.ClaimTypes.Role actually serializes
// to in the JWT (confirmed against a real token from the backend) - it is
// NOT the xmlsoap.org URI used for the other claim types.
const ROLE_CLAIM = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';

@Injectable({
  providedIn: 'root'
})
export class AuthService {

  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  register(request: RegisterRequest): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(
      `${this.apiUrl}/api/Auth/register`,
      request
    );
  }

  login(request: LoginRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${this.apiUrl}/api/Auth/login`, request)
      .pipe(
        tap(response => this.storeTokens(response))
      );
  }

  refresh(): Observable<AuthResponse> {
    const refreshToken = this.getRefreshToken();

    return this.http
      .post<AuthResponse>(`${this.apiUrl}/api/Auth/refresh`, { refreshToken })
      .pipe(
        tap(response => this.storeTokens(response))
      );
  }

  logout(): void {
    const refreshToken = this.getRefreshToken();

    this.clearTokens();

    if (refreshToken) {
      this.http
        .post(`${this.apiUrl}/api/Auth/logout`, { refreshToken })
        .subscribe({
          error: () => {
            // Local session is already cleared, ignore server errors.
          }
        });
    }
  }

  isLoggedIn(): boolean {
    return !!this.getAccessToken();
  }

  getAccessToken(): string | null {
    return localStorage.getItem('accessToken');
  }

  getRefreshToken(): string | null {
    return localStorage.getItem('refreshToken');
  }

  getEmail(): string | null {
    const payload = this.decodeToken();
    return payload?.['email'] ?? null;
  }

  getRoles(): string[] {
    const payload = this.decodeToken();

    if (!payload) {
      return [];
    }

    const roles = payload[ROLE_CLAIM];

    if (!roles) {
      return [];
    }

    return Array.isArray(roles) ? roles : [roles];
  }

  isAdmin(): boolean {
    return this.getRoles().includes('Admin');
  }

  isWarehouseManager(): boolean {
    return this.getRoles().includes('WarehouseManager');
  }

  isStaff(): boolean {
    return this.isAdmin() || this.isWarehouseManager();
  }

  private storeTokens(response: AuthResponse): void {
    localStorage.setItem('accessToken', response.accessToken);
    localStorage.setItem('refreshToken', response.refreshToken);
    localStorage.setItem('accessTokenExpiresAt', response.accessTokenExpiresAt);
  }

  private clearTokens(): void {
    localStorage.removeItem('accessToken');
    localStorage.removeItem('refreshToken');
    localStorage.removeItem('accessTokenExpiresAt');
  }

  private decodeToken(): any {
    const token = this.getAccessToken();

    if (!token) {
      return null;
    }

    try {
      const base64 = token
        .split('.')[1]
        .replace(/-/g, '+')
        .replace(/_/g, '/');

      const padded = base64 + '='.repeat((4 - (base64.length % 4)) % 4);

      return JSON.parse(atob(padded));
    } catch {
      return null;
    }
  }
}
