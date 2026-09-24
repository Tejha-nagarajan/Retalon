import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting
} from '@angular/common/http/testing';

import { authInterceptor } from './auth.interceptor';
import { environment } from '../../environments/environment';

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting()
      ]
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('attaches the access token as a Bearer header when one is stored', () => {
    localStorage.setItem('accessToken', 'access-1');

    http.get(`${environment.apiUrl}/api/products/1`).subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/api/products/1`);
    expect(req.request.headers.get('Authorization')).toBe('Bearer access-1');

    req.flush({});
  });

  it('marks every request as not cacheable, so GETs like /api/cart never return stale data', () => {
    http.get(`${environment.apiUrl}/api/cart`).subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/api/cart`);
    expect(req.request.headers.get('Cache-Control')).toBe('no-cache');

    req.flush({ cartId: 'c1', items: [], total: 0 });
  });

  it('does not attach a header when there is no stored token', () => {
    http.get(`${environment.apiUrl}/api/products/1`).subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/api/products/1`);
    expect(req.request.headers.has('Authorization')).toBe(false);

    req.flush({});
  });

  it('refreshes the access token and retries the request after a 401', () => {
    localStorage.setItem('accessToken', 'expired-access');
    localStorage.setItem('refreshToken', 'refresh-1');

    let result: unknown;

    http.get(`${environment.apiUrl}/api/orders`).subscribe(response => {
      result = response;
    });

    const firstReq = httpMock.expectOne(`${environment.apiUrl}/api/orders`);
    expect(firstReq.request.headers.get('Authorization')).toBe(
      'Bearer expired-access'
    );
    firstReq.flush({ message: 'Unauthorized' }, { status: 401, statusText: 'Unauthorized' });

    const refreshReq = httpMock.expectOne(`${environment.apiUrl}/api/Auth/refresh`);
    expect(refreshReq.request.body).toEqual({ refreshToken: 'refresh-1' });
    refreshReq.flush({
      accessToken: 'new-access',
      refreshToken: 'new-refresh',
      accessTokenExpiresAt: '2026-01-01T00:00:00Z'
    });

    const retryReq = httpMock.expectOne(`${environment.apiUrl}/api/orders`);
    expect(retryReq.request.headers.get('Authorization')).toBe('Bearer new-access');
    retryReq.flush([{ orderId: 1 }]);

    expect(result).toEqual([{ orderId: 1 }]);
    expect(localStorage.getItem('accessToken')).toBe('new-access');
  });

  it('logs out and propagates the error when the refresh itself fails', () => {
    localStorage.setItem('accessToken', 'expired-access');
    localStorage.setItem('refreshToken', 'refresh-1');

    let failed = false;

    http.get(`${environment.apiUrl}/api/orders`).subscribe({
      next: () => {
        throw new Error('expected the request to error out');
      },
      error: () => {
        failed = true;
      }
    });

    const firstReq = httpMock.expectOne(`${environment.apiUrl}/api/orders`);
    firstReq.flush({}, { status: 401, statusText: 'Unauthorized' });

    const refreshReq = httpMock.expectOne(`${environment.apiUrl}/api/Auth/refresh`);
    refreshReq.flush({}, { status: 401, statusText: 'Unauthorized' });

    // logout() fires a best-effort revoke call using the (still present)
    // refresh token before clearing local storage.
    const logoutReq = httpMock.expectOne(`${environment.apiUrl}/api/Auth/logout`);
    logoutReq.flush({});

    expect(failed).toBe(true);
    expect(localStorage.getItem('accessToken')).toBeNull();
    expect(localStorage.getItem('refreshToken')).toBeNull();
  });

  it('does not try to refresh when the auth endpoints themselves return 401', () => {
    localStorage.setItem('accessToken', 'bad-access');

    let failed = false;

    http
      .post(`${environment.apiUrl}/api/Auth/login`, { email: 'a', password: 'b' })
      .subscribe({
        next: () => {
          throw new Error('expected the request to error out');
        },
        error: () => {
          failed = true;
        }
      });

    const req = httpMock.expectOne(`${environment.apiUrl}/api/Auth/login`);
    req.flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(failed).toBe(true);
    httpMock.expectNone(`${environment.apiUrl}/api/Auth/refresh`);
  });
});
