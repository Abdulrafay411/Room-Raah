import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { LoginComponent } from './login.component';
import { AuthService } from '../../../core/auth/auth.service';
import { AuthSessionDto } from '../../../core/auth/auth.model';

describe('LoginComponent', () => {
  let component: LoginComponent;
  let fixture: ComponentFixture<LoginComponent>;
  let authServiceSpy: { login: ReturnType<typeof vi.fn> };
  let router: Router;

  const mockSessionVerified: AuthSessionDto = {
    accessToken: 'valid-token',
    refreshToken: 'valid-refresh',
    expiresInSeconds: 1800,
    user: {
      id: 6,
      fullName: 'Hamza Iqbal',
      email: 'seeker1@roomraah.local',
      role: 'Seeker',
      isEmailVerified: true,
    },
  };

  const mockSessionUnverified: AuthSessionDto = {
    ...mockSessionVerified,
    user: {
      ...mockSessionVerified.user,
      isEmailVerified: false,
    },
  };

  beforeEach(async () => {
    authServiceSpy = { login: vi.fn() };

    await TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: authServiceSpy },
      ],
    }).compileComponents();

    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate');

    fixture = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should populate form when fillSeedAccount is called', () => {
    component.fillSeedAccount('seeker1@roomraah.local');
    expect(component.loginForm.value.email).toBe('seeker1@roomraah.local');
    expect(component.loginForm.value.password).toBe('Password1');
  });

  it('should sign in and navigate to home for verified seeker', () => {
    authServiceSpy.login.mockReturnValue(of(mockSessionVerified));

    component.loginForm.patchValue({
      email: 'seeker1@roomraah.local',
      password: 'Password1',
    });

    component.onSubmit();

    expect(authServiceSpy.login).toHaveBeenCalledWith({
      email: 'seeker1@roomraah.local',
      password: 'Password1',
    });
    expect(router.navigate).toHaveBeenCalledWith(['/']);
  });

  it('should navigate to /verify when unverified user logs in', () => {
    authServiceSpy.login.mockReturnValue(of(mockSessionUnverified));

    component.loginForm.patchValue({
      email: 'seeker.unverified@roomraah.local',
      password: 'Password1',
    });

    component.onSubmit();

    expect(router.navigate).toHaveBeenCalledWith(['/verify'], {
      queryParams: { email: 'seeker1@roomraah.local' },
    });
  });

  it('should show error message on 401 response', () => {
    authServiceSpy.login.mockReturnValue(
      throwError(() => ({
        status: 401,
        error: {
          success: false,
          statusCode: 401,
          error: 'Invalid email or password.',
        },
      }))
    );

    component.loginForm.patchValue({
      email: 'wrong@example.com',
      password: 'WrongPassword',
    });

    component.onSubmit();

    expect(component.errorMessage()).toBe('Invalid email or password.');
  });
});
