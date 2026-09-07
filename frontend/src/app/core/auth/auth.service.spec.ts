import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { AuthService } from './auth.service';
import { AuthSessionDto } from './auth.model';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;
  let routerSpy: { navigate: ReturnType<typeof vi.fn> };

  const sampleToken =
    'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.' +
    btoa(
      JSON.stringify({
        sub: '6',
        email: 'seeker1@roomraah.local',
        email_verified: 'true',
        'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': 'Seeker',
        exp: 2500000000,
      })
    ) +
    '.signature';

  const mockSession: AuthSessionDto = {
    accessToken: sampleToken,
    refreshToken: 'refresh-token-123',
    expiresInSeconds: 1800,
    user: {
      id: 6,
      fullName: 'Hamza Iqbal',
      email: 'seeker1@roomraah.local',
      role: 'Seeker',
      isEmailVerified: true,
    },
  };

  beforeEach(() => {
    localStorage.clear();
    routerSpy = { navigate: vi.fn() };

    TestBed.configureTestingModule({
      providers: [
        AuthService,
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: Router, useValue: routerSpy },
      ],
    });

    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should store tokens and update signals on login', () => {
    service.login({ email: 'seeker1@roomraah.local', password: 'Password1' }).subscribe((session) => {
      expect(session).toEqual(mockSession);
    });

    const req = httpMock.expectOne('http://52.72.119.254:8080/api/v1/auth/login');
    expect(req.request.method).toBe('POST');
    req.flush(mockSession);

    expect(service.accessToken).toBe(sampleToken);
    expect(service.refreshToken).toBe('refresh-token-123');
    expect(service.isAuthenticated()).toBe(true);
    expect(service.isEmailVerified()).toBe(true);
    expect(service.userRole()).toBe('Seeker');
    expect(localStorage.getItem('roomraah_access_token')).toBe(sampleToken);
  });

  it('should verify OTP and establish session immediately', () => {
    service.verifyOtp({ email: 'seeker1@roomraah.local', code: '123456' }).subscribe((session) => {
      expect(session).toEqual(mockSession);
    });

    const req = httpMock.expectOne('http://52.72.119.254:8080/api/v1/auth/verify-otp');
    expect(req.request.method).toBe('POST');
    req.flush(mockSession);

    expect(service.isAuthenticated()).toBe(true);
    expect(service.currentUser()?.fullName).toBe('Hamza Iqbal');
  });

  it('should rotate tokens on refreshSession', () => {
    service.setSession(mockSession);

    const rotatedSession: AuthSessionDto = {
      ...mockSession,
      accessToken: sampleToken + '_rotated',
      refreshToken: 'rotated-refresh-token-456',
    };

    service.refreshSession().subscribe((res) => {
      expect(res.refreshToken).toBe('rotated-refresh-token-456');
    });

    const req = httpMock.expectOne('http://52.72.119.254:8080/api/v1/auth/refresh');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ refreshToken: 'refresh-token-123' });
    req.flush(rotatedSession);

    expect(service.refreshToken).toBe('rotated-refresh-token-456');
    expect(localStorage.getItem('roomraah_refresh_token')).toBe('rotated-refresh-token-456');
  });

  it('should clear session and navigate to login on logout', () => {
    service.setSession(mockSession);
    expect(service.isAuthenticated()).toBe(true);

    service.logout().subscribe();

    const req = httpMock.expectOne('http://52.72.119.254:8080/api/v1/auth/logout');
    expect(req.request.method).toBe('POST');
    req.flush({});

    expect(service.accessToken).toBeNull();
    expect(service.isAuthenticated()).toBe(false);
    expect(localStorage.getItem('roomraah_access_token')).toBeNull();
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/login']);
  });
});
