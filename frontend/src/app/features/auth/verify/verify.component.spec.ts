import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { VerifyComponent } from './verify.component';
import { AuthService } from '../../../core/auth/auth.service';
import { AuthSessionDto } from '../../../core/auth/auth.model';

describe('VerifyComponent', () => {
  let component: VerifyComponent;
  let fixture: ComponentFixture<VerifyComponent>;
  let authServiceSpy: {
    verifyOtp: ReturnType<typeof vi.fn>;
    resendOtp: ReturnType<typeof vi.fn>;
    currentUser: ReturnType<typeof vi.fn>;
  };
  let router: Router;

  const mockSession: AuthSessionDto = {
    accessToken: 'verified-access-token',
    refreshToken: 'verified-refresh-token',
    expiresInSeconds: 1800,
    user: {
      id: 6,
      fullName: 'Hamza Iqbal',
      email: 'seeker1@roomraah.local',
      role: 'Seeker',
      isEmailVerified: true,
    },
  };

  beforeEach(async () => {
    authServiceSpy = {
      verifyOtp: vi.fn(),
      resendOtp: vi.fn(),
      currentUser: vi.fn().mockReturnValue(null),
    };

    await TestBed.configureTestingModule({
      imports: [VerifyComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: authServiceSpy },
      ],
    }).compileComponents();

    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate');

    fixture = TestBed.createComponent(VerifyComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should verify OTP and navigate immediately to home for seeker', () => {
    authServiceSpy.verifyOtp.mockReturnValue(of(mockSession));

    component.verifyForm.patchValue({
      email: 'seeker1@roomraah.local',
      code: '123456',
    });

    component.onSubmit();

    expect(authServiceSpy.verifyOtp).toHaveBeenCalledWith({
      email: 'seeker1@roomraah.local',
      code: '123456',
    });
    expect(router.navigate).toHaveBeenCalledWith(['/']);
  });

  it('should show error when verification code fails', () => {
    authServiceSpy.verifyOtp.mockReturnValue(
      throwError(() => ({
        status: 400,
        error: {
          success: false,
          statusCode: 400,
          error: 'Invalid or expired verification code.',
        },
      }))
    );

    component.verifyForm.patchValue({
      email: 'seeker1@roomraah.local',
      code: '000000',
    });

    component.onSubmit();

    expect(component.errorMessage()).toBe('Invalid or expired verification code.');
  });

  it('should handle 429 rate limit with retryAfterSeconds on resend', () => {
    authServiceSpy.resendOtp.mockReturnValue(
      throwError(() => ({
        status: 429,
        error: {
          success: false,
          statusCode: 429,
          error: 'Too many requests.',
          retryAfterSeconds: 45,
        },
      }))
    );

    component.verifyForm.patchValue({
      email: 'seeker1@roomraah.local',
    });

    component.resendCode();

    expect(component.errorMessage()).toContain('Please wait 45 seconds');
    expect(component.resendCooldownSeconds()).toBe(45);
  });
});
