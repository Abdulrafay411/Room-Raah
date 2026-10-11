import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ADMIN_PAGE_SIZE, AdminService } from '../../../core/services/admin.service';
import {
  AdminProperty,
  AdminPropertySummary,
  AdminVerificationCheck,
  CHECK_TYPES,
  requiredNote,
} from '../../../core/models/admin.model';
import { PropertyStatus } from '../../../core/models/owner.model';
import { VerificationCheckType, VerificationStatus } from '../../../core/models/catalog.model';
import { toFailure } from '../../../core/http/api-error';
import { ButtonComponent } from '../../../shared/components';
import {
  AdminPagerComponent,
  AdminStatusComponent,
  AdminWorkspaceComponent,
  CHECK_LABELS,
  ConfirmDialogComponent,
  ConfirmRequest,
  REQUIRED_CHECKS,
  isRequiredCheck,
  statusLabel,
} from '../shared';

type ListingAction = 'approve' | 'reject' | 'request-changes' | 'unpublish';

const STATUS_FILTERS: { value: PropertyStatus | ''; label: string }[] = [
  { value: '', label: 'All listings' },
  { value: 'PendingReview', label: 'Pending review' },
  { value: 'Published', label: 'Published' },
  { value: 'ChangesRequested', label: 'Changes requested' },
  { value: 'Rejected', label: 'Rejected' },
  { value: 'Unpublished', label: 'Unpublished' },
  { value: 'Draft', label: 'Draft' },
];

@Component({
  selector: 'app-admin-listings',
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
  templateUrl: './admin-listings.component.html',
  styleUrls: ['./admin-listings.component.scss'],
})
export class AdminListingsComponent {
  private readonly api = inject(AdminService);

  readonly items = signal<AdminPropertySummary[]>([]);
  readonly page = signal(1);
  readonly total = signal(0);
  readonly pageSize = ADMIN_PAGE_SIZE;

  readonly selected = signal<AdminProperty | null>(null);
  readonly loading = signal(true);
  readonly detailLoading = signal(false);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);
  readonly confirm = signal<ConfirmRequest | null>(null);

  readonly status = signal<PropertyStatus | ''>('');
  readonly statusFilters = STATUS_FILTERS;
  readonly adminNote = signal('');

  /** Which check's form is open under its row, if any. One at a time. */
  readonly editingCheck = signal<VerificationCheckType | null>(null);
  readonly checkStatus = signal<VerificationStatus>('Checked');
  readonly evidenceDate = signal('');
  readonly checkNote = signal('');

  readonly checkTypes = CHECK_TYPES;
  readonly checkLabels = CHECK_LABELS;
  readonly isRequired = isRequiredCheck;
  readonly today = new Date().toISOString().slice(0, 10);

  /** "3 of 4" for the checks that gate publishing. */
  readonly requiredPassed = computed(() => {
    const detail = this.selected();
    if (!detail) return 0;
    return REQUIRED_CHECKS.filter((type) => this.checkFor(detail, type)?.status === 'Checked').length;
  });
  readonly requiredTotal = REQUIRED_CHECKS.length;

  constructor() {
    this.load();
  }

  checkFor(detail: AdminProperty, type: VerificationCheckType): AdminVerificationCheck | null {
    return detail.verificationChecks.find((check) => check.checkType === type) ?? null;
  }

  label(status: string): string {
    return statusLabel(status);
  }

  facilityNames(detail: AdminProperty): string {
    return detail.facilities.map((facility) => facility.name).join(', ');
  }

  /** Photographs in the order a seeker sees them: the main one first. */
  photosInOrder(detail: AdminProperty) {
    return [...detail.photos].sort(
      (a, b) => Number(b.isPrimary) - Number(a.isPrimary) || a.sortOrder - b.sortOrder,
    );
  }

  filter(status: PropertyStatus | ''): void {
    this.status.set(status);
    this.page.set(1);
    this.load();
  }

  goTo(page: number): void {
    this.page.set(page);
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.properties(this.status() || undefined, this.page()).subscribe({
      next: (page) => {
        this.items.set(page.items ?? []);
        this.total.set(page.totalCount ?? page.items?.length ?? 0);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(toFailure(err).message);
        this.loading.set(false);
      },
    });
  }

  open(row: AdminPropertySummary): void {
    this.detailLoading.set(true);
    this.error.set(null);
    this.notice.set(null);
    this.editingCheck.set(null);
    this.adminNote.set('');
    this.api.property(row.id).subscribe({
      next: (detail) => {
        this.selected.set(detail);
        this.detailLoading.set(false);
      },
      error: (err) => {
        this.error.set(toFailure(err).message);
        this.detailLoading.set(false);
      },
    });
  }

  close(): void {
    this.selected.set(null);
    this.editingCheck.set(null);
    this.confirm.set(null);
  }

  editCheck(detail: AdminProperty, type: VerificationCheckType): void {
    if (this.editingCheck() === type) {
      this.editingCheck.set(null);
      return;
    }
    const existing = this.checkFor(detail, type);
    this.checkStatus.set(existing?.status ?? 'Checked');
    this.evidenceDate.set(existing?.evidenceDate ? existing.evidenceDate.slice(0, 10) : '');
    this.checkNote.set(existing?.note ?? '');
    this.editingCheck.set(type);
  }

  recordCheck(): void {
    const detail = this.selected();
    const type = this.editingCheck();
    if (!detail || !type) return;

    this.saving.set(true);
    this.error.set(null);
    this.notice.set(null);
    this.api
      .verify(detail.id, {
        checkType: type,
        status: this.checkStatus(),
        evidenceDate: this.evidenceDate()
          ? new Date(`${this.evidenceDate()}T00:00:00`).toISOString()
          : null,
        note: this.checkNote().trim() || null,
      })
      .subscribe({
        next: () => {
          this.notice.set(`${CHECK_LABELS[type].name} recorded as ${statusLabel(this.checkStatus()).toLowerCase()}.`);
          this.editingCheck.set(null);
          this.saving.set(false);
          this.reloadSelected(detail.id);
        },
        error: (err) => this.fail(err),
      });
  }

  /** A button was pressed. Check the note, then either ask first or go ahead. */
  request(action: ListingAction): void {
    const detail = this.selected();
    if (!detail) return;
    if (action !== 'approve') {
      const problem = requiredNote(this.adminNote());
      if (problem) {
        this.error.set(problem);
        return;
      }
    }
    this.error.set(null);

    if (action === 'reject') {
      this.confirm.set({
        title: 'Reject this listing?',
        description: `"${detail.title}" is sent back to the owner as rejected, with your note. They can change it and send it again.`,
        quote: this.adminNote(),
        confirmLabel: 'Reject listing',
        run: () => this.decide('reject'),
      });
      return;
    }
    if (action === 'unpublish') {
      this.confirm.set({
        title: 'Unpublish this listing?',
        description: `"${detail.title}" disappears from search and its page stops working for seekers. The owner is told why.`,
        quote: this.adminNote(),
        confirmLabel: 'Unpublish listing',
        run: () => this.decide('unpublish'),
      });
      return;
    }
    this.decide(action);
  }

  private decide(action: ListingAction): void {
    const detail = this.selected();
    if (!detail) return;

    this.saving.set(true);
    this.error.set(null);
    this.notice.set(null);
    this.api.decideProperty(detail.id, action, this.adminNote()).subscribe({
      next: (updated) => {
        this.selected.set(updated);
        this.adminNote.set('');
        this.confirm.set(null);
        this.notice.set(this.actionCopy(action));
        this.saving.set(false);
        this.load();
      },
      error: (err) => {
        this.confirm.set(null);
        this.fail(err);
      },
    });
  }

  private reloadSelected(id: number): void {
    this.api.property(id).subscribe({
      next: (detail) => this.selected.set(detail),
      error: (err) => this.fail(err),
    });
  }

  private actionCopy(action: ListingAction): string {
    if (action === 'approve') return 'Listing published. It is in search now.';
    if (action === 'unpublish') return 'Listing unpublished. It is out of search.';
    if (action === 'request-changes') return 'Changes requested. The owner has your note.';
    return 'Listing rejected. The owner has your note.';
  }

  private fail(err: unknown): void {
    this.error.set(toFailure(err).message);
    this.saving.set(false);
  }
}
