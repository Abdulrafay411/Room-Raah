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
            queryParams: { email: session.user.email },
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

  // Quick fill seed accounts (§3 of brief) for developer testing
  fillSeedAccount(email: string): void {
    this.loginForm.patchValue({
      email,
      password: 'Password1',
    });
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
