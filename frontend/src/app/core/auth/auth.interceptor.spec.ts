import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { Subject, of, throwError } from 'rxjs';
import { _resetInterceptorState, authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';
import { AuthSessionDto } from './auth.model';

describe('authInterceptor', () => {
  let httpClient: HttpClient;
  let httpMock: HttpTestingController;
  let authService: AuthService;
  let routerSpy: { navigate: ReturnType<typeof vi.fn> };

  const sampleToken = 'initial-token';
  const newRefreshedToken = 'refreshed-token';

  beforeEach(() => {
    _resetInterceptorState();
    routerSpy = { navigate: vi.fn() };

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: Router, useValue: routerSpy },
      ],
    });

    httpClient = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    authService = TestBed.inject(AuthService);
  });

  afterEach(() => {
    httpMock.verify();
    _resetInterceptorState();
  });

  it('should attach Bearer token to API requests when authenticated', () => {
    authService.accessTokenSignal.set(sampleToken);

    httpClient.get('http://52.72.119.254:8080/api/v1/properties').subscribe();

    const req = httpMock.expectOne('http://52.72.119.254:8080/api/v1/properties');
    expect(req.request.headers.get('Authorization')).toBe(`Bearer ${sampleToken}`);
    req.flush({ items: [] });
  });

  it('should not attach token to /auth/login request', () => {
    authService.accessTokenSignal.set(sampleToken);

    httpClient.post('http://52.72.119.254:8080/api/v1/auth/login', {}).subscribe();

    const req = httpMock.expectOne('http://52.72.119.254:8080/api/v1/auth/login');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({});
  });

  it('should refresh token and retry on 401 response', () => {
    authService.accessTokenSignal.set(sampleToken);
    authService.refreshTokenSignal.set('refresh-123');

    const refreshSpy = vi.spyOn(authService, 'refreshSession').mockReturnValue(
      of({
        accessToken: newRefreshedToken,
        refreshToken: 'refresh-456',
        expiresInSeconds: 1800,
        user: { id: 1, fullName: 'Test', email: 'test@test.local', role: 'Seeker', isEmailVerified: true },
      } as AuthSessionDto)
    );

    httpClient.get('http://52.72.119.254:8080/api/v1/properties').subscribe();

    // 1st request fails with 401
    const firstReq = httpMock.expectOne('http://52.72.119.254:8080/api/v1/properties');
    firstReq.flush({ error: 'Unauthorized' }, { status: 401, statusText: 'Unauthorized' });

    expect(refreshSpy).toHaveBeenCalled();

    // Retry request with refreshed token
    const retryReq = httpMock.expectOne('http://52.72.119.254:8080/api/v1/properties');
    expect(retryReq.request.headers.get('Authorization')).toBe(`Bearer ${newRefreshedToken}`);
    retryReq.flush({ items: [] });
  });

  it('should route to /verify when receiving 403 for unverified user', () => {
    authService.accessTokenSignal.set(sampleToken);
    // Unverified user:
    vi.spyOn(authService, 'isEmailVerified').mockReturnValue(false);

    httpClient.post('http://52.72.119.254:8080/api/v1/saved/1', {}).subscribe({
      error: () => {},
    });

    const req = httpMock.expectOne('http://52.72.119.254:8080/api/v1/saved/1');
    req.flush({ error: 'Forbidden' }, { status: 403, statusText: 'Forbidden' });

    expect(routerSpy.navigate).toHaveBeenCalledWith(['/verify']);
  });

  it('should queue concurrent 401 requests and retry all after single refresh', () => {
    authService.accessTokenSignal.set(sampleToken);
    authService.refreshTokenSignal.set('refresh-123');

    const refreshSubject = new Subject<AuthSessionDto>();
    const refreshSpy = vi.spyOn(authService, 'refreshSession').mockReturnValue(refreshSubject.asObservable());

    let req1Result: any = null;
    let req2Result: any = null;

    httpClient.get('http://52.72.119.254:8080/api/v1/properties').subscribe((res) => (req1Result = res));
    httpClient.get('http://52.72.119.254:8080/api/v1/areas').subscribe((res) => (req2Result = res));

    // Request 1 fails with 401, triggering silent refresh
    const req1 = httpMock.expectOne('http://52.72.119.254:8080/api/v1/properties');
    req1.flush({ error: 'Unauthorized' }, { status: 401, statusText: 'Unauthorized' });

    expect(refreshSpy).toHaveBeenCalledTimes(1);

    // Request 2 fails with 401 while refresh is in flight; should queue behind refresh
    const req2 = httpMock.expectOne('http://52.72.119.254:8080/api/v1/areas');
    req2.flush({ error: 'Unauthorized' }, { status: 401, statusText: 'Unauthorized' });

    // Still only 1 refresh call should have been made
    expect(refreshSpy).toHaveBeenCalledTimes(1);

    // Emit successful refreshed session
    refreshSubject.next({
      accessToken: newRefreshedToken,
      refreshToken: 'refresh-456',
      expiresInSeconds: 1800,
      user: { id: 1, fullName: 'Test', email: 'test@test.local', role: 'Seeker', isEmailVerified: true },
    });
    refreshSubject.complete();

    // Both requests should now retry with newRefreshedToken
    const retry1 = httpMock.expectOne('http://52.72.119.254:8080/api/v1/properties');
    const retry2 = httpMock.expectOne('http://52.72.119.254:8080/api/v1/areas');

    expect(retry1.request.headers.get('Authorization')).toBe(`Bearer ${newRefreshedToken}`);
    expect(retry2.request.headers.get('Authorization')).toBe(`Bearer ${newRefreshedToken}`);

    retry1.flush({ items: ['prop1'] });
    retry2.flush({ items: ['area1'] });

    expect(req1Result).toEqual({ items: ['prop1'] });
    expect(req2Result).toEqual({ items: ['area1'] });
  });

  it('should reject all queued requests and not hang when refresh fails', () => {
    authService.accessTokenSignal.set(sampleToken);
    authService.refreshTokenSignal.set('refresh-123');

    const refreshSubject = new Subject<AuthSessionDto>();
    vi.spyOn(authService, 'refreshSession').mockReturnValue(refreshSubject.asObservable());

    let req1Error: any = null;
    let req2Error: any = null;

    httpClient.get('http://52.72.119.254:8080/api/v1/properties').subscribe({
      next: () => {},
      error: (err) => (req1Error = err),
    });
    httpClient.get('http://52.72.119.254:8080/api/v1/areas').subscribe({
      next: () => {},
      error: (err) => (req2Error = err),
    });

    // Request 1 fails with 401
    const req1 = httpMock.expectOne('http://52.72.119.254:8080/api/v1/properties');
    req1.flush({ error: 'Unauthorized' }, { status: 401, statusText: 'Unauthorized' });

    // Request 2 fails with 401 and joins queue
    const req2 = httpMock.expectOne('http://52.72.119.254:8080/api/v1/areas');
    req2.flush({ error: 'Unauthorized' }, { status: 401, statusText: 'Unauthorized' });

    // Refresh fails
    refreshSubject.error(new Error('Invalid refresh token'));

    // Both requests must have received the rejection error and NOT hung
    expect(req1Error).toBeTruthy();
    expect(req2Error).toBeTruthy();
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/login']);
  });
});
