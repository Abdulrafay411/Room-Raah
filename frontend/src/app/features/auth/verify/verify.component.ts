import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
import { ApiErrorResponse, VerifyOtpRequest } from '../../../core/auth/auth.model';
import {
  ButtonComponent,
  FormFieldComponent,
  InputDirective,
} from '../../../shared/components';

@Component({
  selector: 'app-verify',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterLink,
    ButtonComponent,
    FormFieldComponent,
    InputDirective,
  ],
  templateUrl: './verify.component.html',
  styleUrls: ['./verify.component.scss'],
})
export class VerifyComponent implements OnInit, OnDestroy {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  verifyForm: FormGroup = this.fb.group({
    email: ['', [Validators.required, Validators.email]],
    code: ['', [Validators.required, Validators.pattern(/^\d{6}$/)]],
  });

  isLoading = signal<boolean>(false);
  isResending = signal<boolean>(false);
  errorMessage = signal<string>('');
  infoMessage = signal<string>('');

  resendCooldownSeconds = signal<number>(0);
  private timerInterval: any = null;

  ngOnInit(): void {
    const queryEmail = this.route.snapshot.queryParams['email'];
    const currentEmail = this.authService.currentUser()?.email;
    const resolvedEmail = queryEmail || currentEmail || '';

    if (resolvedEmail) {
      this.verifyForm.patchValue({ email: resolvedEmail });
    }
  }

  ngOnDestroy(): void {
    if (this.timerInterval) {
      clearInterval(this.timerInterval);
    }
  }

  onSubmit(): void {
    this.errorMessage.set('');
    this.infoMessage.set('');

    if (this.verifyForm.invalid) {
      this.verifyForm.markAllAsTouched();
      return;
    }

    this.isLoading.set(true);
    const payload: VerifyOtpRequest = {
      email: this.verifyForm.value.email.trim(),
      code: this.verifyForm.value.code.trim(),
    };

    this.authService.verifyOtp(payload).subscribe({
      next: (session) => {
        this.isLoading.set(false);
        // Brief §4.1: session is established immediately upon verification
        if (session.user.role === 'Owner') {
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
        } else if (err.status === 400 || err.status === 422) {
          this.errorMessage.set('Invalid or expired verification code.');
        } else {
          this.errorMessage.set('Verification failed. Please try again.');
        }
      },
    });
  }

  resendCode(): void {
    const email = this.verifyForm.get('email')?.value?.trim();
    if (!email) {
      this.errorMessage.set('Please enter your email address to request a new code.');
      return;
    }

    this.isResending.set(true);
    this.errorMessage.set('');
    this.infoMessage.set('');

    this.authService.resendOtp({ email }).subscribe({
      next: () => {
        this.isResending.set(false);
        this.infoMessage.set('A fresh 6-digit code has been sent to your email.');
        this.startCooldown(60);
      },
      error: (err) => {
        this.isResending.set(false);
        const errResp = err.error as ApiErrorResponse;
        if (err.status === 429 && errResp?.retryAfterSeconds) {
          this.errorMessage.set(
            `Too many requests. Please wait ${errResp.retryAfterSeconds} seconds before requesting another code.`
          );
          this.startCooldown(errResp.retryAfterSeconds);
        } else if (errResp?.error) {
          this.errorMessage.set(errResp.error);
        } else {
          this.errorMessage.set('Could not resend verification code. Please try again.');
        }
      },
    });
  }

  private startCooldown(seconds: number): void {
    this.resendCooldownSeconds.set(seconds);
    if (this.timerInterval) clearInterval(this.timerInterval);

    this.timerInterval = setInterval(() => {
      const remaining = this.resendCooldownSeconds() - 1;
      if (remaining <= 0) {
        clearInterval(this.timerInterval);
        this.resendCooldownSeconds.set(0);
      } else {
        this.resendCooldownSeconds.set(remaining);
      }
    }, 1000);
  }

  getFieldError(fieldName: string): string {
    const control = this.verifyForm.get(fieldName);
    if (control && control.touched && control.errors) {
      if (control.errors['required']) return 'This field is required';
      if (control.errors['email']) return 'Please enter a valid email address';
      if (control.errors['pattern']) return 'Verification code must be exactly 6 digits';
    }
    return '';
  }
}
