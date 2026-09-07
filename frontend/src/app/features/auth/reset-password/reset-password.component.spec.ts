import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ResetPasswordComponent } from './reset-password.component';
import { AuthService } from '../../../core/auth/auth.service';

describe('ResetPasswordComponent', () => {
  let component: ResetPasswordComponent;
  let fixture: ComponentFixture<ResetPasswordComponent>;
  let authServiceSpy: {
    forgotPassword: ReturnType<typeof vi.fn>;
    resetPassword: ReturnType<typeof vi.fn>;
  };

  beforeEach(async () => {
    authServiceSpy = {
      forgotPassword: vi.fn(),
      resetPassword: vi.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [ResetPasswordComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: authServiceSpy },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ResetPasswordComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should request reset link in request mode', () => {
    authServiceSpy.forgotPassword.mockReturnValue(of({}));

    component.forgotForm.patchValue({
      email: 'seeker1@roomraah.local',
    });

    component.onRequestSubmit();

    expect(authServiceSpy.forgotPassword).toHaveBeenCalledWith({
      email: 'seeker1@roomraah.local',
    });
    expect(component.successMessage()).toContain('reset link has been dispatched');
  });

  it('should reset password with token in reset mode', () => {
    authServiceSpy.resetPassword.mockReturnValue(of({}));

    component.setMode('reset');
    component.resetForm.patchValue({
      email: 'seeker1@roomraah.local',
      token: 'valid-reset-token',
      newPassword: 'NewPassword1',
    });

    component.onResetSubmit();

    expect(authServiceSpy.resetPassword).toHaveBeenCalledWith({
      email: 'seeker1@roomraah.local',
      token: 'valid-reset-token',
      newPassword: 'NewPassword1',
    });
    expect(component.successMessage()).toContain('password has been reset successfully');
  });

  it('should display error message on failure', () => {
    authServiceSpy.forgotPassword.mockReturnValue(
      throwError(() => ({
        error: {
          success: false,
          statusCode: 400,
          error: 'Email address not found.',
        },
      }))
    );

    component.forgotForm.patchValue({
      email: 'nonexistent@example.com',
    });

    component.onRequestSubmit();

    expect(component.errorMessage()).toBe('Email address not found.');
  });
});
