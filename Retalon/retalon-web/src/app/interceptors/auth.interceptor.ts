import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {

  const authService = inject(AuthService);

  const token = authService.getAccessToken();

  // GET responses (e.g. the cart) must never be served from the browser's
  // HTTP cache - the same URL can legitimately return different data on
  // every request (after adding/removing items), so caching it would show
  // stale results.
  const headers: Record<string, string> = { 'Cache-Control': 'no-cache' };

  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  const authReq = req.clone({ setHeaders: headers });

  const isAuthEndpoint = req.url.toLowerCase().includes('/api/auth/');

  return next(authReq).pipe(
    catchError((error: HttpErrorResponse) => {

      const canRefresh =
        error.status === 401 &&
        !isAuthEndpoint &&
        !!authService.getRefreshToken();

      if (!canRefresh) {
        return throwError(() => error);
      }

      // Access tokens are short-lived, so a 401 usually just means
      // it expired mid-session. Try to refresh it once and retry.
      return authService.refresh().pipe(
        switchMap(() => {
          const retryReq = req.clone({
            setHeaders: {
              'Cache-Control': 'no-cache',
              Authorization: `Bearer ${authService.getAccessToken()}`
            }
          });

          return next(retryReq);
        }),
        catchError(refreshError => {
          authService.logout();
          return throwError(() => refreshError);
        })
      );
    })
  );
};
