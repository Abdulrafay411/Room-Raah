import { HttpErrorResponse, HttpHandlerFn, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { BehaviorSubject, Observable, catchError, filter, switchMap, take, throwError } from 'rxjs';
import { AuthService } from './auth.service';
import { environment } from '../../../environments/environment';

let isRefreshing = false;
let refreshTokenSubject = new BehaviorSubject<string | null>(null);

export function _resetInterceptorState(): void {
  isRefreshing = false;
  refreshTokenSubject = new BehaviorSubject<string | null>(null);
}

export const authInterceptor: HttpInterceptorFn = (req: HttpRequest<unknown>, next: HttpHandlerFn): Observable<any> => {
  const authService = inject(AuthService);
  const router = inject(Router);

  const isApiRequest = req.url.startsWith(environment.apiBaseUrl);
  const isAuthEndpoint =
    req.url.includes('/auth/login') ||
    req.url.includes('/auth/register') ||
    req.url.includes('/auth/refresh') ||
    req.url.includes('/auth/forgot-password') ||
    req.url.includes('/auth/reset-password');

  let authReq = req;
  const token = authService.accessToken;

  // Attach Bearer token to API requests (except unauthenticated auth endpoints)
  if (isApiRequest && token && !isAuthEndpoint) {
    authReq = req.clone({
      setHeaders: {
        Authorization: `Bearer ${token}`,
      },
    });
  }

  return next(authReq).pipe(
    catchError((error: HttpErrorResponse) => {
      // 1. Handle 401 Unauthorized with Silent Refresh Queue
      if (error.status === 401 && isApiRequest && !isAuthEndpoint) {
        return handle401Error(authReq, next, authService, router, error);
      }

      // 2. Handle 403 Forbidden for unverified accounts (Brief §1.3 & §10.2)
      // Only a refused *write* means "go and verify". Rule 2 is that an unverified account
      // may browse but not act, and a read answers 403 too - the shortlist prime that every
      // public page fires is one. Redirecting on that dragged an unverified account off the
      // landing page, off search and off every listing, which is the opposite of the rule.
      if (error.status === 403 && isApiRequest && req.method !== 'GET') {
        if (!authService.isEmailVerified()) {
          router.navigate(['/verify']);
        }
      }

      return throwError(() => error);
    })
  );
};

function handle401Error(
  req: HttpRequest<unknown>,
  next: HttpHandlerFn,
  authService: AuthService,
  router: Router,
  originalError: HttpErrorResponse
): Observable<any> {
  if (!authService.refreshToken) {
    authService.clearSession();
    router.navigate(['/login']);
    return throwError(() => originalError);
  }

  if (!isRefreshing) {
    isRefreshing = true;
    refreshTokenSubject = new BehaviorSubject<string | null>(null);

    return authService.refreshSession().pipe(
      switchMap((newSession) => {
        isRefreshing = false;
        refreshTokenSubject.next(newSession.accessToken);

        // Retry original failed request with the new access token
        const retryReq = req.clone({
          setHeaders: {
            Authorization: `Bearer ${newSession.accessToken}`,
          },
        });
        return next(retryReq);
      }),
      catchError((refreshErr) => {
        const subjectToNotify = refreshTokenSubject;
        isRefreshing = false;
        refreshTokenSubject = new BehaviorSubject<string | null>(null);
        authService.clearSession();
        router.navigate(['/login']);
        subjectToNotify.error(refreshErr);
        return throwError(() => refreshErr);
      })
    );
  } else {
    // Another refresh is already in-flight: queue this request and await the new token
    return refreshTokenSubject.pipe(
      filter((newToken): newToken is string => newToken !== null),
      take(1),
      switchMap((newToken) => {
        const retryReq = req.clone({
          setHeaders: {
            Authorization: `Bearer ${newToken}`,
          },
        });
        return next(retryReq);
      })
    );
  }
}
