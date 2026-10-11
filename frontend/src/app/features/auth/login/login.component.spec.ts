import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { LoginComponent } from './login.component';
import { AuthService } from '../../../core/auth/auth.service';
import { AuthSessionDto } from '../../../core/auth/auth.model';
import { TestAccountsService } from '../../../core/services/test-accounts.service';
import { AdminUserSummary } from '../../../core/models/admin.model';

describe('LoginComponent', () => {
  let component: LoginComponent;
  let fixture: ComponentFixture<LoginComponent>;
  let authServiceSpy: { login: ReturnType<typeof vi.fn> };
  let testAccountsSpy: { accounts: ReturnType<typeof vi.fn> };
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
    testAccountsSpy = { accounts: vi.fn() };

    await TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: authServiceSpy },
        { provide: TestAccountsService, useValue: testAccountsSpy },
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

  it('lists the accounts of the picked role for quick sign-in', () => {
    const account: AdminUserSummary = {
      id: 4,
      fullName: 'Sana Malik',
      email: 'sana.malik@roomraah.local',
      role: 'Owner',
      isEmailVerified: true,
      isActive: true,
      createdAt: '2026-10-11T00:00:00Z',
    };
    testAccountsSpy.accounts.mockReturnValue(of([account]));

    component.pickType('owner');

    expect(testAccountsSpy.accounts).toHaveBeenCalledWith('Owner');
    expect(component.quickAccounts()).toEqual([account]);
    expect(component.accountLabel({ ...account, isEmailVerified: false })).toContain('(unverified)');
  });

  it('splits seekers into verified and unverified', () => {
    const base: AdminUserSummary = {
      id: 7,
      fullName: 'Ali Raza',
      email: 'ali.raza@roomraah.local',
      role: 'Seeker',
      isEmailVerified: true,
      isActive: true,
      createdAt: '2026-10-11T00:00:00Z',
    };
    const unverified = { ...base, id: 8, email: 'bilal.unverified@roomraah.local', isEmailVerified: false };
    testAccountsSpy.accounts.mockReturnValue(of([base, unverified]));

    component.pickType('seeker');
    expect(component.quickAccounts()).toEqual([base]);

    component.pickType('seeker-unverified');
    expect(testAccountsSpy.accounts).toHaveBeenLastCalledWith('Seeker');
    expect(component.quickAccounts()).toEqual([unverified]);
  });

  it('fills the seed password only for seed accounts', () => {
    component.pickAccount('sana.malik@roomraah.local');
    expect(component.loginForm.value.email).toBe('sana.malik@roomraah.local');
    expect(component.loginForm.value.password).toBe('Password1');

    component.pickAccount('someone@gmail.com');
    expect(component.loginForm.value.email).toBe('someone@gmail.com');
    expect(component.loginForm.value.password).toBe('');
    expect(component.quickHint()).toContain('type its password');
  });

  it('says so when the accounts cannot be loaded', () => {
    testAccountsSpy.accounts.mockReturnValue(throwError(() => ({ status: 0 })));

    component.pickType('seeker');

    expect(component.quickError()).toBe('Could not load accounts from the backend.');
    expect(component.quickLoading()).toBe(false);
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
