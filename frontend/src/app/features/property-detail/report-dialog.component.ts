import { Component, EventEmitter, Input, Output, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import {
  ButtonComponent,
  DialogComponent,
  FormFieldComponent,
  InputDirective,
} from '../../shared/components';
import { EngagementService } from '../../core/services/engagement.service';
import { AuthService } from '../../core/auth/auth.service';
import { toFailure } from '../../core/http/api-error';
import { REPORT_DETAILS_MAX, REPORT_REASONS, ReportReason } from '../../core/models/engagement.model';

/**
 * Reporting a listing, as a dialog on the listing itself rather than a page of its own.
 * That is the brief's call and it is the right one: moving somebody off the listing to fill
 * in one short form is how a report stops being filed.
 *
 * One unresolved report per listing per person, so a second is a 422 whose sentence says
 * exactly that. It is shown as sent.
 */
@Component({
  selector: 'app-report-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    DialogComponent,
    ButtonComponent,
    FormFieldComponent,
    InputDirective,
  ],
  templateUrl: './report-dialog.component.html',
  styleUrls: ['./report-dialog.component.scss'],
})
export class ReportDialogComponent {
  private readonly engagement = inject(EngagementService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  @Input() isOpen = false;
  @Input() propertyId = 0;
  @Output() closed = new EventEmitter<void>();

  readonly reason = signal<ReportReason>('Inaccurate');
  readonly details = signal('');
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);
  readonly sent = signal(false);

  readonly reasons = REPORT_REASONS;
  readonly detailsMax = REPORT_DETAILS_MAX;

  close(): void {
    this.closed.emit();
    // Reset only after it closes, so the dialog does not visibly empty itself on the way out.
    this.error.set(null);
    this.sent.set(false);
    this.details.set('');
  }

  submit(): void {
    if (!this.auth.isAuthenticated()) {
      this.router.navigate(['/login'], { queryParams: { returnUrl: `/property/${this.propertyId}` } });
      return;
    }

    const details = this.details().trim();
    if (!details) {
      this.error.set('Tell us what is wrong so somebody can check it.');
      return;
    }

    this.submitting.set(true);
    this.error.set(null);

    this.engagement.fileReport({ propertyId: this.propertyId, reason: this.reason(), details }).subscribe({
      next: () => {
        this.sent.set(true);
        this.submitting.set(false);
      },
      error: (err) => {
        const failure = toFailure(err);
        if (failure.needsVerification) {
          this.router.navigate(['/verify']);
          return;
        }
        this.error.set(failure.message);
        this.submitting.set(false);
      },
    });
  }
}
