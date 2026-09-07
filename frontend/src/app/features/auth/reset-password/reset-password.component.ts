import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
import { ApiErrorResponse } from '../../../core/auth/auth.model';
import {
  ButtonComponent,
  FormFieldComponent,
  InputDirective,
} from '../../../shared/components';

@Component({
  selector: 'app-reset-password',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterLink,
    ButtonComponent,
    FormFieldComponent,
    InputDirective,
  ],
  templateUrl: './reset-password.component.html',
  styleUrls: ['./reset-password.component.scss'],
})
export class ResetPasswordComponent implements OnInit {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private route = inject(ActivatedRoute);

  // 'request' mode (forgot-password) vs 'reset' mode (reset-password with token)
  mode = signal<'request' | 'reset'>('request');

  forgotForm: FormGroup = this.fb.group({
    email: ['', [Validators.required, Validators.email]],
  });

  resetForm: FormGroup = this.fb.group({
    email: ['', [Validators.required, Validators.email]],
    token: ['', [Validators.required]],
    newPassword: [
      '',
      [
        Validators.required,
        Validators.minLength(8),
        Validators.pattern(/^(?=.*[A-Z])(?=.*\d).*$/),
      ],
    ],
  });

  isLoading = signal<boolean>(false);
  errorMessage = signal<string>('');
  successMessage = signal<string>('');

  ngOnInit(): void {
    const queryEmail = this.route.snapshot.queryParams['email'];
    const queryToken = this.route.snapshot.queryParams['token'];

    if (queryToken) {
      this.mode.set('reset');
      this.resetForm.patchValue({
        token: queryToken,
        email: queryEmail || '',
      });
    } else if (queryEmail) {
      this.forgotForm.patchValue({ email: queryEmail });
    }
  }

  setMode(newMode: 'request' | 'reset'): void {
    this.mode.set(newMode);
    this.errorMessage.set('');
    this.successMessage.set('');
  }

  onRequestSubmit(): void {
    this.errorMessage.set('');
    this.successMessage.set('');

    if (this.forgotForm.invalid) {
      this.forgotForm.markAllAsTouched();
      return;
    }

    this.isLoading.set(true);
    const email = this.forgotForm.value.email.trim();

    this.authService.forgotPassword({ email }).subscribe({
      next: () => {
        this.isLoading.set(false);
        this.successMessage.set(
          `If an account exists for ${email}, a password reset link has been dispatched to Mailpit.`
        );
      },
      error: (err) => {
        this.isLoading.set(false);
        const errResp = err.error as ApiErrorResponse;
        this.errorMessage.set(errResp?.error || 'Could not process password reset request.');
      },
    });
  }

  onResetSubmit(): void {
    this.errorMessage.set('');
    this.successMessage.set('');

    if (this.resetForm.invalid) {
      this.resetForm.markAllAsTouched();
      return;
    }

    this.isLoading.set(true);
    const formValue = this.resetForm.value;

    this.authService
      .resetPassword({
        email: formValue.email.trim(),
        token: formValue.token.trim(),
        newPassword: formValue.newPassword,
      })
      .subscribe({
        next: () => {
          this.isLoading.set(false);
          this.successMessage.set('Your password has been reset successfully. You may now sign in.');
        },
        error: (err) => {
          this.isLoading.set(false);
          const errResp = err.error as ApiErrorResponse;
          this.errorMessage.set(errResp?.error || 'Password reset failed. Invalid or expired token.');
        },
      });
  }

  getForgotError(field: string): string {
    const control = this.forgotForm.get(field);
    if (control && control.touched && control.errors) {
      if (control.errors['required']) return 'Email is required';
      if (control.errors['email']) return 'Enter a valid email address';
    }
    return '';
  }

  getResetError(field: string): string {
    const control = this.resetForm.get(field);
    if (control && control.touched && control.errors) {
      if (control.errors['required']) return 'This field is required';
      if (control.errors['email']) return 'Enter a valid email address';
      if (control.errors['minlength']) return 'Password must be at least 8 characters';
      if (control.errors['pattern']) return 'Must contain at least 1 uppercase letter and 1 digit';
    }
    return '';
  }
}
