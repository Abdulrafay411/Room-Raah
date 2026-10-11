import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ADMIN_PAGE_SIZE, AdminService } from '../../../core/services/admin.service';
import {
  ReportRow,
  ReportStatus,
  ReviewQueueRow,
  ReviewState,
  requiredNote,
} from '../../../core/models/admin.model';
import { toFailure } from '../../../core/http/api-error';
import { ButtonComponent } from '../../../shared/components';
import {
  AdminPagerComponent,
  AdminStatusComponent,
  AdminWorkspaceComponent,
  ConfirmDialogComponent,
  ConfirmRequest,
} from '../shared';

const REPORT_FILTERS: { value: ReportStatus | ''; label: string }[] = [
  { value: '', label: 'All reports' },
  { value: 'Open', label: 'Open' },
  { value: 'UnderReview', label: 'Under review' },
  { value: 'Upheld', label: 'Upheld' },
  { value: 'Dismissed', label: 'Dismissed' },
];

const REVIEW_FILTERS: { value: ReviewState | ''; label: string }[] = [
  { value: 'Pending', label: 'Waiting for approval' },
  { value: 'Approved', label: 'Approved' },
  { value: 'Hidden', label: 'Hidden' },
  { value: '', label: 'All reviews' },
];

@Component({
  selector: 'app-admin-reports',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ButtonComponent,
    AdminWorkspaceComponent,
    AdminStatusComponent,
    AdminPagerComponent,
    ConfirmDialogComponent,
  ],
  templateUrl: './admin-reports.component.html',
  styleUrls: ['./admin-reports.component.scss'],
})
export class AdminReportsComponent {
  private readonly api = inject(AdminService);

  readonly reports = signal<ReportRow[]>([]);
  readonly reportPage = signal(1);
  readonly reportTotal = signal(0);
  readonly reviews = signal<ReviewQueueRow[]>([]);
  readonly reviewPage = signal(1);
  readonly reviewTotal = signal(0);
  readonly pageSize = ADMIN_PAGE_SIZE;

  readonly selectedReport = signal<ReportRow | null>(null);
  readonly reportStatus = signal<ReportStatus | ''>('');
  readonly reviewState = signal<ReviewState | ''>('Pending');
  readonly reportFilters = REPORT_FILTERS;
  readonly reviewFilters = REVIEW_FILTERS;
  readonly note = signal('');
  readonly loadingReports = signal(true);
  readonly loadingReviews = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);
  readonly confirm = signal<ConfirmRequest | null>(null);

  constructor() {
    this.loadReports();
    this.loadReviews();
  }

  filterReports(status: ReportStatus | ''): void {
    this.reportStatus.set(status);
    this.reportPage.set(1);
    this.loadReports();
  }

  goToReports(page: number): void {
    this.reportPage.set(page);
    this.loadReports();
  }

  filterReviews(state: ReviewState | ''): void {
    this.reviewState.set(state);
    this.reviewPage.set(1);
    this.loadReviews();
  }

  goToReviews(page: number): void {
    this.reviewPage.set(page);
    this.loadReviews();
  }

  loadReports(): void {
    this.loadingReports.set(true);
    this.error.set(null);
    this.api.reports(this.reportStatus() || undefined, this.reportPage()).subscribe({
      next: (page) => {
        this.reports.set(page.items ?? []);
        this.reportTotal.set(page.totalCount ?? page.items?.length ?? 0);
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
    this.api.reviews(this.reviewState() || undefined, this.reviewPage()).subscribe({
      next: (page) => {
        this.reviews.set(page.items ?? []);
        this.reviewTotal.set(page.totalCount ?? page.items?.length ?? 0);
        this.loadingReviews.set(false);
      },
      error: (err) => {
        this.error.set(toFailure(err).message);
        this.loadingReviews.set(false);
      },
    });
  }

  open(report: ReportRow): void {
    this.selectedReport.set(report);
    this.note.set('');
    this.error.set(null);
    this.notice.set(null);
  }

  close(): void {
    this.selectedReport.set(null);
    this.confirm.set(null);
  }

  /** Marking under review is a bookmark. Upholding or dismissing is the verdict, and final. */
  request(status: Exclude<ReportStatus, 'Open'>): void {
    const report = this.selectedReport();
    if (!report) return;
    if (status === 'UnderReview') {
      this.resolve('UnderReview');
      return;
    }
    const problem = requiredNote(this.note());
    if (problem) {
      this.error.set(problem);
      return;
    }
    this.error.set(null);
    this.confirm.set(
      status === 'Upheld'
        ? {
            title: 'Uphold this report?',
            description: `The report on "${report.propertyTitle}" is recorded as true. The listing stays public unless you unpublish it separately from the Listings tab. This cannot be changed afterwards.`,
            quote: this.note(),
            confirmLabel: 'Uphold report',
            run: () => this.resolve('Upheld'),
          }
        : {
            title: 'Dismiss this report?',
            description: `The report on "${report.propertyTitle}" is closed with no action. This cannot be changed afterwards.`,
            quote: this.note(),
            confirmLabel: 'Dismiss report',
            variant: 'primary',
            run: () => this.resolve('Dismissed'),
          },
    );
  }

  private resolve(status: Exclude<ReportStatus, 'Open'>): void {
    const report = this.selectedReport();
    if (!report) return;

    this.saving.set(true);
    this.error.set(null);
    this.notice.set(null);
    this.api.resolveReport(report.id, status, this.note()).subscribe({
      next: (updated) => {
        this.selectedReport.set(updated);
        this.note.set('');
        this.confirm.set(null);
        this.notice.set(
          status === 'Upheld'
            ? 'Report upheld. The listing is still public until it is unpublished from the Listings tab.'
            : status === 'Dismissed'
              ? 'Report dismissed.'
              : 'Report marked as under review.',
        );
        this.saving.set(false);
        this.loadReports();
      },
      error: (err) => {
        this.confirm.set(null);
        this.fail(err);
      },
    });
  }

  /** Approving puts a review on the page; hiding takes it off, so hiding asks first. */
  requestModeration(review: ReviewQueueRow, state: 'Approved' | 'Hidden'): void {
    if (state === 'Approved') {
      this.moderate(review, 'Approved');
      return;
    }
    this.confirm.set({
      title: 'Hide this review?',
      description: `${review.authorDisplayName}'s ${review.rating}/5 review of "${review.propertyTitle}" comes off the listing page and stays hidden.`,
      quote: review.comment,
      confirmLabel: 'Hide review',
      run: () => this.moderate(review, 'Hidden'),
    });
  }

  private moderate(review: ReviewQueueRow, state: 'Approved' | 'Hidden'): void {
    this.saving.set(true);
    this.error.set(null);
    this.notice.set(null);
    this.api.moderateReview(review.id, state).subscribe({
      next: () => {
        this.confirm.set(null);
        this.notice.set(state === 'Approved' ? 'Review approved. It is on the listing page now.' : 'Review hidden.');
        this.saving.set(false);
        this.loadReviews();
      },
      error: (err) => {
        this.confirm.set(null);
        this.fail(err);
      },
    });
  }

  private fail(err: unknown): void {
    this.error.set(toFailure(err).message);
    this.saving.set(false);
  }
}
