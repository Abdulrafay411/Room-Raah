import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
import { ApiErrorResponse, LoginRequest } from '../../../core/auth/auth.model';
import {
  ButtonComponent,
  FormFieldComponent,
  InputDirective,
} from '../../../shared/components';
import { TestAccountsService, knownPassword } from '../../../core/services/test-accounts.service';
import { AccountRole, AdminUserSummary } from '../../../core/models/admin.model';
import { verifyParams } from '../../../core/auth/guards/session';

interface QuickType {
  key: string;
  label: string;
  role: AccountRole;
  /** Only accounts with this verification state; left out, every account of the role. */
  verified?: boolean;
}

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterLink,
    ButtonComponent,
    FormFieldComponent,
    InputDirective,
  ],
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.scss'],
})
export class LoginComponent {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private testAccounts = inject(TestAccountsService);

  loginForm: FormGroup = this.fb.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]],
  });

  isLoading = signal<boolean>(false);
  errorMessage = signal<string>('');

  get returnUrl(): string {
    return this.route.snapshot.queryParams['returnUrl'] || '/';
  }

  onSubmit(): void {
    this.errorMessage.set('');

    if (this.loginForm.invalid) {
      this.loginForm.markAllAsTouched();
      return;
    }

    this.isLoading.set(true);
    const credentials: LoginRequest = {
      email: this.loginForm.value.email.trim(),
      password: this.loginForm.value.password,
    };

    this.authService.login(credentials).subscribe({
      next: (session) => {
        this.isLoading.set(false);
        // If email is not verified, route directly to /verify
        if (!session.user.isEmailVerified) {
          this.router.navigate(['/verify'], {
            queryParams: verifyParams({ email: session.user.email, returnUrl: this.returnUrl }),
          });
          return;
        }

        // Navigate based on role or returnUrl
        if (this.returnUrl && this.returnUrl !== '/') {
          this.router.navigateByUrl(this.returnUrl);
        } else if (session.user.role === 'Owner') {
          this.router.navigate(['/owner']);
        } else if (session.user.role === 'Admin') {
          this.router.navigate(['/admin/listings']);
        } else {
          this.router.navigate(['/']);
        }
      },
      error: (err) => {
        this.isLoading.set(false);
        const errResp = err.error as ApiErrorResponse;
        if (errResp?.error) {
          this.errorMessage.set(errResp.error);
        } else if (err.status === 401) {
          this.errorMessage.set('Invalid email or password.');
        } else {
          this.errorMessage.set('Sign in failed. Please verify your connection.');
        }
      },
    });
  }

  // Quick sign-in, TESTING ONLY: pick a type, then any account of that type in the DB.
  // The admin list cannot filter on verification, so the two seeker types split it here.
  readonly quickTypes: QuickType[] = [
    { key: 'seeker', label: 'Seeker', role: 'Seeker', verified: true },
    { key: 'seeker-unverified', label: 'Seeker (unverified)', role: 'Seeker', verified: false },
    { key: 'owner', label: 'Owner', role: 'Owner' },
    { key: 'admin', label: 'Admin', role: 'Admin' },
  ];
  quickType = signal<QuickType | null>(null);
  quickEmail = signal('');
  quickAccounts = signal<AdminUserSummary[]>([]);
  quickLoading = signal(false);
  quickError = signal('');
  quickHint = signal('');

  pickType(key: string): void {
    const type = this.quickTypes.find((t) => t.key === key) ?? null;
    this.quickType.set(type);
    this.quickEmail.set('');
    this.quickAccounts.set([]);
    this.quickError.set('');
    this.quickHint.set('');
    if (!type) return;

    this.quickLoading.set(true);
    this.testAccounts.accounts(type.role).subscribe({
      next: (accounts) => {
        // A late answer for a type the visitor has already switched away from is dropped.
        if (this.quickType() !== type) return;
        this.quickAccounts.set(
          type.verified === undefined ? accounts : accounts.filter((a) => a.isEmailVerified === type.verified),
        );
        this.quickLoading.set(false);
      },
      error: () => {
        if (this.quickType() !== type) return;
        this.quickError.set('Could not load accounts from the backend.');
        this.quickLoading.set(false);
      },
    });
  }

  pickAccount(email: string): void {
    this.quickEmail.set(email);
    if (!email) return;
    const password = knownPassword(email);
    this.loginForm.patchValue({ email, password: password ?? '' });
    this.quickHint.set(password ? '' : 'This account was registered by a person - type its password.');
  }

  accountLabel(account: AdminUserSummary): string {
    const notes = [!account.isEmailVerified && 'unverified', !account.isActive && 'deactivated'].filter(Boolean);
    return `${account.fullName} - ${account.email}${notes.length ? ` (${notes.join(', ')})` : ''}`;
  }

  getFieldError(fieldName: string): string {
    const control = this.loginForm.get(fieldName);
    if (control && control.touched && control.errors) {
      if (control.errors['required']) return 'This field is required';
      if (control.errors['email']) return 'Please enter a valid email address';
    }
    return '';
  }
}
