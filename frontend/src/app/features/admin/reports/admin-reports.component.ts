import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../../../core/services/admin.service';
import {
  ReportRow,
  ReportStatus,
  ReviewQueueRow,
  ReviewState,
  requiredNote,
} from '../../../core/models/admin.model';
import { toFailure } from '../../../core/http/api-error';
import { ButtonComponent } from '../../../shared/components';

@Component({
  selector: 'app-admin-reports',
  standalone: true,
  imports: [CommonModule, FormsModule, ButtonComponent],
  templateUrl: './admin-reports.component.html',
  styleUrls: ['./admin-reports.component.scss'],
})
export class AdminReportsComponent {
  private readonly api = inject(AdminService);

  readonly reports = signal<ReportRow[]>([]);
  readonly reviews = signal<ReviewQueueRow[]>([]);
  readonly selectedReport = signal<ReportRow | null>(null);
  readonly reportStatus = signal<ReportStatus | ''>('');
  readonly reviewState = signal<ReviewState | ''>('Pending');
  readonly note = signal('');
  readonly loadingReports = signal(true);
  readonly loadingReviews = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);

  constructor() {
    this.loadReports();
    this.loadReviews();
  }

  loadReports(): void {
    this.loadingReports.set(true);
    this.error.set(null);
    this.api.reports(this.reportStatus() || undefined).subscribe({
      next: (page) => {
        this.reports.set(page.items ?? []);
        this.loadingReports.set(false);
      },
      error: (err) => {
        this.error.set(toFailure(err).message);
        this.loadingReports.set(false);
      },
    });
  }

  loadReviews(): void {
    this.loadingReviews.set(true);
    this.error.set(null);
    this.api.reviews(this.reviewState() || undefined).subscribe({
      next: (page) => {
        this.reviews.set(page.items ?? []);
        this.loadingReviews.set(false);
      },
      error: (err) => {
        this.error.set(toFailure(err).message);
        this.loadingReviews.set(false);
      },
    });
  }

  resolve(status: Exclude<ReportStatus, 'Open'>): void {
    const report = this.selectedReport();
    if (!report) return;
    if (status === 'Upheld' || status === 'Dismissed') {
      const problem = requiredNote(this.note());
      if (problem) {
        this.error.set(problem);
        return;
      }
    }

    this.saving.set(true);
    this.error.set(null);
    this.notice.set(null);
    this.api.resolveReport(report.id, status, this.note()).subscribe({
      next: (updated) => {
        this.selectedReport.set(updated);
        this.note.set('');
        this.notice.set(
          status === 'Upheld'
            ? 'Report upheld. The listing remains public until it is separately unpublished.'
            : `Report moved to ${status}.`,
        );
        this.saving.set(false);
        this.loadReports();
      },
      error: (err) => this.fail(err),
    });
  }

  moderate(review: ReviewQueueRow, state: 'Approved' | 'Hidden'): void {
    this.saving.set(true);
    this.error.set(null);
    this.notice.set(null);
    this.api.moderateReview(review.id, state).subscribe({
      next: () => {
        this.notice.set(state === 'Approved' ? 'Review is now public.' : 'Review hidden from public pages.');
        this.saving.set(false);
        this.loadReviews();
      },
      error: (err) => this.fail(err),
    });
  }

  private fail(err: unknown): void {
    this.error.set(toFailure(err).message);
    this.saving.set(false);
  }
}
