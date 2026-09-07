import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { SignupComponent } from './signup.component';
import { AuthService } from '../../../core/auth/auth.service';

describe('SignupComponent', () => {
  let component: SignupComponent;
  let fixture: ComponentFixture<SignupComponent>;
  let authServiceSpy: { register: ReturnType<typeof vi.fn> };
  let router: Router;

  beforeEach(async () => {
    authServiceSpy = { register: vi.fn() };

    await TestBed.configureTestingModule({
      imports: [SignupComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: authServiceSpy },
      ],
    }).compileComponents();

    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate');

    fixture = TestBed.createComponent(SignupComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should start with Seeker role selected and no CNIC validation', () => {
    expect(component.selectedRole()).toBe('Seeker');
    const cnicControl = component.signupForm.get('cnicNumber');
    expect(cnicControl?.validator).toBeNull();
  });

  it('should require CNIC when switching to Owner role', () => {
    component.setRole('Owner');
    fixture.detectChanges();

    expect(component.selectedRole()).toBe('Owner');
    const cnicControl = component.signupForm.get('cnicNumber');
    expect(cnicControl?.validator).toBeTruthy();

    cnicControl?.setValue('123'); // invalid length
    expect(cnicControl?.valid).toBe(false);

    cnicControl?.setValue('3520112345673'); // 13 digits
    expect(cnicControl?.valid).toBe(true);
  });

  it('should submit registration and navigate to /verify on success', () => {
    authServiceSpy.register.mockReturnValue(of({}));

    component.signupForm.patchValue({
      fullName: 'Hamza Iqbal',
      email: 'seeker1@roomraah.local',
      phoneNumber: '+15551234567',
      password: 'Password1',
      role: 'Seeker',
    });

    component.onSubmit();

    expect(authServiceSpy.register).toHaveBeenCalledWith({
      fullName: 'Hamza Iqbal',
      email: 'seeker1@roomraah.local',
      phoneNumber: '+15551234567',
      password: 'Password1',
      role: 'Seeker',
    });

    expect(router.navigate).toHaveBeenCalledWith(['/verify'], {
      queryParams: { email: 'seeker1@roomraah.local' },
    });
  });

  it('should display server error message on failure', () => {
    authServiceSpy.register.mockReturnValue(
      throwError(() => ({
        error: {
          success: false,
          statusCode: 400,
          error: 'Email already registered.',
        },
      }))
    );

    component.signupForm.patchValue({
      fullName: 'Hamza Iqbal',
      email: 'seeker1@roomraah.local',
      phoneNumber: '+15551234567',
      password: 'Password1',
      role: 'Seeker',
    });

    component.onSubmit();

    expect(component.generalError()).toBe('Email already registered.');
  });

  it('should validate phone number per contract (digits and optional +, max 20 chars)', () => {
    const phoneControl = component.signupForm.get('phoneNumber');

    // Valid formats
    phoneControl?.setValue('+15551234567');
    expect(phoneControl?.valid).toBe(true);

    phoneControl?.setValue('12345678');
    expect(phoneControl?.valid).toBe(true);

    phoneControl?.setValue('+1234567890123456789'); // 20 chars
    expect(phoneControl?.valid).toBe(true);

    // Invalid formats
    phoneControl?.setValue('');
    expect(phoneControl?.valid).toBe(false);

    phoneControl?.setValue('not-a-number');
    expect(phoneControl?.valid).toBe(false);

    phoneControl?.setValue('+123456789012345678901'); // 22 chars (> 20)
    expect(phoneControl?.valid).toBe(false);
  });
});

