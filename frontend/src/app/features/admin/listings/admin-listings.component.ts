import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../../../core/services/admin.service';
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

type ListingAction = 'approve' | 'reject' | 'request-changes' | 'unpublish';

@Component({
  selector: 'app-admin-listings',
  standalone: true,
  imports: [CommonModule, FormsModule, ButtonComponent],
  templateUrl: './admin-listings.component.html',
  styleUrls: ['./admin-listings.component.scss'],
})
export class AdminListingsComponent {
  private readonly api = inject(AdminService);

  readonly items = signal<AdminPropertySummary[]>([]);
  readonly selected = signal<AdminProperty | null>(null);
  readonly loading = signal(true);
  readonly detailLoading = signal(false);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);

  readonly status = signal<PropertyStatus | ''>('');
  readonly adminNote = signal('');
  readonly checkType = signal<VerificationCheckType>('OwnerIdentity');
  readonly checkStatus = signal<VerificationStatus>('Checked');
  readonly evidenceDate = signal('');
  readonly checkNote = signal('');

  readonly checkTypes = CHECK_TYPES;
  readonly today = new Date().toISOString().slice(0, 10);

  constructor() {
    this.load();
  }

  checkFor(detail: AdminProperty, type: VerificationCheckType): AdminVerificationCheck | null {
    return detail.verificationChecks.find((check) => check.checkType === type) ?? null;
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.properties(this.status() || undefined).subscribe({
      next: (page) => {
        this.items.set(page.items ?? []);
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

  recordCheck(): void {
    const detail = this.selected();
    if (!detail) return;

    this.saving.set(true);
    this.error.set(null);
    this.notice.set(null);
    this.api
      .verify(detail.id, {
        checkType: this.checkType(),
        status: this.checkStatus(),
        evidenceDate: this.evidenceDate()
          ? new Date(`${this.evidenceDate()}T00:00:00`).toISOString()
          : null,
        note: this.checkNote().trim() || null,
      })
      .subscribe({
        next: () => {
          this.notice.set('Verification check recorded.');
          this.saving.set(false);
          this.reloadSelected(detail.id);
        },
        error: (err) => this.fail(err),
      });
  }

  decide(action: ListingAction): void {
    const detail = this.selected();
    if (!detail) return;
    if (action !== 'approve') {
      const problem = requiredNote(this.adminNote());
      if (problem) {
        this.error.set(problem);
        return;
      }
    }

    this.saving.set(true);
    this.error.set(null);
    this.notice.set(null);
    this.api.decideProperty(detail.id, action, this.adminNote()).subscribe({
      next: (updated) => {
        this.selected.set(updated);
        this.adminNote.set('');
        this.notice.set(this.actionCopy(action));
        this.saving.set(false);
        this.load();
      },
      error: (err) => this.fail(err),
    });
  }

  private reloadSelected(id: number): void {
    this.api.property(id).subscribe({
      next: (detail) => this.selected.set(detail),
      error: (err) => this.fail(err),
    });
  }

  private actionCopy(action: ListingAction): string {
    if (action === 'approve') return 'Listing published.';
    if (action === 'unpublish') return 'Listing removed from public search.';
    if (action === 'request-changes') return 'Changes requested from the owner.';
    return 'Listing rejected.';
  }

  private fail(err: unknown): void {
    this.error.set(toFailure(err).message);
    this.saving.set(false);
  }
}
