import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting
} from '@angular/common/http/testing';

import { AuthService } from './auth.service';
import { environment } from '../../environments/environment';

const ROLE_CLAIM = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';

function base64url(value: string): string {
  return btoa(value)
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '');
}

function makeToken(payload: Record<string, unknown>): string {
  const header = base64url(JSON.stringify({ alg: 'none', typ: 'JWT' }));
  const body = base64url(JSON.stringify(payload));
  return `${header}.${body}.signature`;
}

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('is not logged in when there is no stored token', () => {
    expect(service.isLoggedIn()).toBe(false);
  });

  it('register posts the full registration payload to /api/Auth/register', () => {
    service
      .register({
        firstName: 'Ada',
        lastName: 'Lovelace',
        email: 'ada@example.com',
        password: 'password123',
        addressLine1: '1 Main St',
        city: 'London',
        postalCode: 'AB1',
        country: 'UK'
      })
      .subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/api/Auth/register`);

    expect(req.request.method).toBe('POST');
    expect(req.request.body.email).toBe('ada@example.com');
    expect(req.request.body.firstName).toBe('Ada');

    req.flush({ message: 'User registered successfully.' });
  });

  it('login stores the returned tokens and reports as logged in', () => {
    service
      .login({ email: 'ada@example.com', password: 'password123' })
      .subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/api/Auth/login`);

    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      email: 'ada@example.com',
      password: 'password123'
    });

    req.flush({
      accessToken: 'access-1',
      refreshToken: 'refresh-1',
      accessTokenExpiresAt: '2026-01-01T00:00:00Z'
    });

    expect(service.isLoggedIn()).toBe(true);
    expect(service.getAccessToken()).toBe('access-1');
    expect(service.getRefreshToken()).toBe('refresh-1');
  });

  it('refresh sends the stored refresh token and stores the new tokens', () => {
    localStorage.setItem('accessToken', 'old-access');
    localStorage.setItem('refreshToken', 'old-refresh');

    service.refresh().subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/api/Auth/refresh`);

    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ refreshToken: 'old-refresh' });

    req.flush({
      accessToken: 'new-access',
      refreshToken: 'new-refresh',
      accessTokenExpiresAt: '2026-01-01T00:00:00Z'
    });

    expect(service.getAccessToken()).toBe('new-access');
    expect(service.getRefreshToken()).toBe('new-refresh');
  });

  it('logout clears local tokens immediately and revokes the refresh token', () => {
    localStorage.setItem('accessToken', 'access-1');
    localStorage.setItem('refreshToken', 'refresh-1');

    service.logout();

    // The session must be cleared locally without waiting for the server.
    expect(service.isLoggedIn()).toBe(false);
    expect(service.getRefreshToken()).toBeNull();

    const req = httpMock.expectOne(`${environment.apiUrl}/api/Auth/logout`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ refreshToken: 'refresh-1' });

    req.flush({ message: 'Logged out successfully.' });
  });

  it('logout does not call the API when there is no refresh token', () => {
    service.logout();
    httpMock.expectNone(`${environment.apiUrl}/api/Auth/logout`);
  });

  it('decodes roles out of the access token', () => {
    const token = makeToken({
      email: 'admin@example.com',
      [ROLE_CLAIM]: ['Admin', 'WarehouseManager']
    });

    localStorage.setItem('accessToken', token);

    expect(service.getRoles()).toEqual(['Admin', 'WarehouseManager']);
    expect(service.isAdmin()).toBe(true);
    expect(service.isWarehouseManager()).toBe(true);
    expect(service.isStaff()).toBe(true);
    expect(service.getEmail()).toBe('admin@example.com');
  });

  it('handles a single (non-array) role claim, as issued for one-role users', () => {
    const token = makeToken({
      email: 'customer@example.com',
      [ROLE_CLAIM]: 'Customer'
    });

    localStorage.setItem('accessToken', token);

    expect(service.getRoles()).toEqual(['Customer']);
    expect(service.isAdmin()).toBe(false);
    expect(service.isStaff()).toBe(false);
  });

  it('returns no roles when there is no token', () => {
    expect(service.getRoles()).toEqual([]);
    expect(service.isStaff()).toBe(false);
  });
});
