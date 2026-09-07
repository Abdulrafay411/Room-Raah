import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, catchError, map, of, tap, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AuthSessionDto,
  AuthUserDto,
  DecodedToken,
  ForgotPasswordRequest,
  LoginRequest,
  RegisterRequest,
  ResetPasswordRequest,
  ResendOtpRequest,
  UserRole,
  VerifyOtpRequest,
} from './auth.model';
import { decodeJwt, isTokenExpired } from './jwt.util';

const ACCESS_TOKEN_KEY = 'roomraah_access_token';
const REFRESH_TOKEN_KEY = 'roomraah_refresh_token';
const USER_KEY = 'roomraah_user';

@Injectable({
  providedIn: 'root',
})
export class AuthService {
  private http = inject(HttpClient);
  private router = inject(Router);

  private readonly apiBase = environment.apiBaseUrl;

  // Reactive state signals
  readonly accessTokenSignal = signal<string | null>(this.getStoredAccessToken());
  readonly refreshTokenSignal = signal<string | null>(this.getStoredRefreshToken());
  readonly currentUser = signal<AuthUserDto | null>(this.getStoredUser());

  // Computed signals
  readonly decodedToken = computed<DecodedToken | null>(() => {
    const token = this.accessTokenSignal();
    return token ? decodeJwt(token) : null;
  });

  readonly isAuthenticated = computed<boolean>(() => {
    const token = this.accessTokenSignal();
    return !!token && !isTokenExpired(token);
  });

  readonly isEmailVerified = computed<boolean>(() => {
    // Check decoded token string first per contract
    const decoded = this.decodedToken();
    if (decoded) {
      return decoded.email_verified === 'true';
    }
    return this.currentUser()?.isEmailVerified === true;
  });

  readonly userRole = computed<UserRole | null>(() => {
    return this.decodedToken()?.role ?? this.currentUser()?.role ?? null;
  });

  get accessToken(): string | null {
    return this.accessTokenSignal();
  }

  get refreshToken(): string | null {
    return this.refreshTokenSignal();
  }

  register(payload: RegisterRequest): Observable<unknown> {
    return this.http.post(`${this.apiBase}/auth/register`, payload);
  }

  login(payload: LoginRequest): Observable<AuthSessionDto> {
    return this.http
      .post<AuthSessionDto>(`${this.apiBase}/auth/login`, payload)
      .pipe(tap((session) => this.setSession(session)));
  }

  verifyOtp(payload: VerifyOtpRequest): Observable<AuthSessionDto> {
    return this.http
      .post<AuthSessionDto>(`${this.apiBase}/auth/verify-otp`, payload)
      .pipe(tap((session) => this.setSession(session)));
  }

  resendOtp(payload: ResendOtpRequest): Observable<unknown> {
    return this.http.post(`${this.apiBase}/auth/resend-otp`, payload);
  }

  /**
   * Refreshes the session. Note: the backend rotates the refresh token.
   */
  refreshSession(): Observable<AuthSessionDto> {
    const currentRefreshToken = this.refreshToken;
    if (!currentRefreshToken) {
      this.clearSession();
      return throwError(() => new Error('No refresh token available'));
    }

    return this.http
      .post<AuthSessionDto>(`${this.apiBase}/auth/refresh`, {
        refreshToken: currentRefreshToken,
      })
      .pipe(
        tap((newSession) => this.setSession(newSession)),
        catchError((err) => {
          this.clearSession();
          return throwError(() => err);
        })
      );
  }

  logout(): Observable<unknown> {
    const currentRefreshToken = this.refreshToken;
    const request$ = currentRefreshToken
      ? this.http.post(`${this.apiBase}/auth/logout`, { refreshToken: currentRefreshToken }).pipe(
          catchError(() => of(null)) // Clean up client even if backend rejects
        )
      : of(null);

    return request$.pipe(
      tap(() => {
        this.clearSession();
        this.router.navigate(['/login']);
      })
    );
  }

  forgotPassword(payload: ForgotPasswordRequest): Observable<unknown> {
    return this.http.post(`${this.apiBase}/auth/forgot-password`, payload);
  }

  resetPassword(payload: ResetPasswordRequest): Observable<unknown> {
    return this.http.post(`${this.apiBase}/auth/reset-password`, payload);
  }

  setSession(session: AuthSessionDto): void {
    if (typeof localStorage !== 'undefined') {
      localStorage.setItem(ACCESS_TOKEN_KEY, session.accessToken);
      localStorage.setItem(REFRESH_TOKEN_KEY, session.refreshToken);
      localStorage.setItem(USER_KEY, JSON.stringify(session.user));
    }

    this.accessTokenSignal.set(session.accessToken);
    this.refreshTokenSignal.set(session.refreshToken);
    this.currentUser.set(session.user);
  }

  clearSession(): void {
    if (typeof localStorage !== 'undefined') {
      localStorage.removeItem(ACCESS_TOKEN_KEY);
      localStorage.removeItem(REFRESH_TOKEN_KEY);
      localStorage.removeItem(USER_KEY);
    }

    this.accessTokenSignal.set(null);
    this.refreshTokenSignal.set(null);
    this.currentUser.set(null);
  }

  private getStoredAccessToken(): string | null {
    if (typeof localStorage !== 'undefined') {
      return localStorage.getItem(ACCESS_TOKEN_KEY);
    }
    return null;
  }

  private getStoredRefreshToken(): string | null {
    if (typeof localStorage !== 'undefined') {
      return localStorage.getItem(REFRESH_TOKEN_KEY);
    }
    return null;
  }

  private getStoredUser(): AuthUserDto | null {
    if (typeof localStorage !== 'undefined') {
      const userJson = localStorage.getItem(USER_KEY);
      if (userJson) {
        try {
          return JSON.parse(userJson) as AuthUserDto;
        } catch {
          return null;
        }
      }
    }
    return null;
  }
}
