import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
import { ApiErrorResponse, RegisterRequest } from '../../../core/auth/auth.model';
import {
  ButtonComponent,
  FormFieldComponent,
  InputDirective,
} from '../../../shared/components';

@Component({
  selector: 'app-signup',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterLink,
    ButtonComponent,
    FormFieldComponent,
    InputDirective,
  ],
  templateUrl: './signup.component.html',
  styleUrls: ['./signup.component.scss'],
})
export class SignupComponent {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private router = inject(Router);

  signupForm: FormGroup = this.fb.group({
    fullName: ['', [Validators.required, Validators.maxLength(120)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(200)]],
    phoneNumber: ['', [Validators.required, Validators.maxLength(20), Validators.pattern(/^[+]?\d+$/)]],
    password: [
      '',
      [
        Validators.required,
        Validators.minLength(8),
        Validators.pattern(/^(?=.*[A-Z])(?=.*\d).*$/), // at least 1 uppercase and 1 digit
      ],
    ],
    role: ['Seeker', [Validators.required]],
    cnicNumber: [''],
  });

  selectedRole = signal<'Seeker' | 'Owner'>('Seeker');
  isLoading = signal<boolean>(false);
  generalError = signal<string>('');
  fieldErrors = signal<Record<string, string>>({});

  constructor() {
    this.signupForm.get('role')?.valueChanges.subscribe((role: 'Seeker' | 'Owner') => {
      this.selectedRole.set(role);
      const cnicControl = this.signupForm.get('cnicNumber');
      if (role === 'Owner') {
        cnicControl?.setValidators([Validators.required, Validators.pattern(/^\d{13}$/)]);
      } else {
        cnicControl?.clearValidators();
        cnicControl?.setValue('');
      }
      cnicControl?.updateValueAndValidity();
    });
  }

  setRole(role: 'Seeker' | 'Owner'): void {
    this.signupForm.patchValue({ role });
  }

  onSubmit(): void {
    this.generalError.set('');
    this.fieldErrors.set({});

    if (this.signupForm.invalid) {
      this.signupForm.markAllAsTouched();
      return;
    }

    this.isLoading.set(true);
    const formValue = this.signupForm.value;

    const payload: RegisterRequest = {
      fullName: formValue.fullName.trim(),
      email: formValue.email.trim(),
      phoneNumber: formValue.phoneNumber.trim(),
      password: formValue.password,
      role: formValue.role,
      ...(formValue.role === 'Owner' ? { cnicNumber: formValue.cnicNumber.trim() } : {}), // guard:allow-owner-contact: the owner's own
    };

    this.authService.register(payload).subscribe({
      next: () => {
        this.isLoading.set(false);
        this.router.navigate(['/verify'], {
          queryParams: { email: payload.email },
        });
      },
      error: (err) => {
        this.isLoading.set(false);
        const errResp = err.error as ApiErrorResponse;

        if (errResp?.errors && Array.isArray(errResp.errors)) {
          const errorsMap: Record<string, string> = {};
          for (const item of errResp.errors) {
            const camelField = item.field.charAt(0).toLowerCase() + item.field.slice(1);
            errorsMap[camelField] = item.message;
          }
          this.fieldErrors.set(errorsMap);
        }

        if (errResp?.error) {
          this.generalError.set(errResp.error);
        } else if (!errResp?.errors) {
          this.generalError.set('Registration failed. Please check your information and try again.');
        }
      },
    });
  }

  getFieldError(fieldName: string): string {
    const serverErr = this.fieldErrors()[fieldName];
    if (serverErr) return serverErr;

    const control = this.signupForm.get(fieldName);
    if (control && control.touched && control.errors) {
      if (control.errors['required']) return 'This field is required';
      if (control.errors['email']) return 'Please enter a valid email address';
      if (control.errors['minlength']) return 'Password must be at least 8 characters';
      if (control.errors['pattern'] && fieldName === 'password')
        return 'Password must contain at least one uppercase letter and one digit';
      if (control.errors['maxlength'] && fieldName === 'phoneNumber')
        return 'Phone number must not exceed 20 characters';
      if (control.errors['pattern'] && fieldName === 'phoneNumber')
        return 'Enter a valid phone number (digits and optional leading +)';
      if (control.errors['pattern'] && fieldName === 'cnicNumber')
        return 'CNIC must be exactly 13 digits without dashes';
    }
    return '';
  }
}
