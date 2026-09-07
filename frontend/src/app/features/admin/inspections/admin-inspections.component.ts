import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../../../core/services/admin.service';
import { AuthService } from '../../../core/auth/auth.service';
import { DueInspection, inspectionProblem, requiredNote } from '../../../core/models/admin.model';
import { toLocalInputValue } from '../../../core/models/engagement.model';
import { toFailure } from '../../../core/http/api-error';
import { ButtonComponent } from '../../../shared/components';

@Component({
  selector: 'app-admin-inspections',
  standalone: true,
  imports: [CommonModule, FormsModule, ButtonComponent],
  templateUrl: './admin-inspections.component.html',
  styleUrls: ['./admin-inspections.component.scss'],
})
export class AdminInspectionsComponent {
  private readonly api = inject(AdminService);
  private readonly auth = inject(AuthService);

  readonly due = signal<DueInspection[]>([]);
  readonly canInspect = signal(false);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);

  readonly propertyId = signal<number | null>(null);
  readonly inspectedAt = signal('');
  readonly result = signal<'Passed' | 'Failed'>('Passed');
  readonly notes = signal('');
  readonly feeAmount = signal<number | null>(null);
  readonly feeCollectedAt = signal('');

  readonly badgePropertyId = signal<number | null>(null);
  readonly badgeReason = signal('');
  readonly maxNow = toLocalInputValue(new Date());

  constructor() {
    this.loadPermission();
    this.loadDue();
  }

  loadDue(): void {
    this.loading.set(true);
    this.api.dueInspections().subscribe({
      next: (page) => {
        this.due.set(page.items ?? []);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(toFailure(err).message);
        this.loading.set(false);
      },
    });
  }

  use(row: DueInspection): void {
    this.propertyId.set(row.propertyId);
    this.badgePropertyId.set(row.propertyId);
  }

  record(): void {
    const id = this.propertyId() ?? 0;
    const problem = inspectionProblem(id, this.inspectedAt());
    if (problem) {
      this.error.set(problem);
      return;
    }
    if (this.feeCollectedAt() && new Date(this.feeCollectedAt()).getTime() > Date.now()) {
      this.error.set('A fee collection date cannot be in the future.');
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    this.notice.set(null);
    this.api
      .recordInspection({
        propertyId: id,
        inspectedAt: new Date(this.inspectedAt()).toISOString(),
        result: this.result(),
        notes: this.notes().trim() || null,
        feeAmount: this.feeAmount(),
        feeCollectedAt: this.feeCollectedAt()
          ? new Date(this.feeCollectedAt()).toISOString()
          : null,
      })
      .subscribe({
        next: () => {
          this.notice.set('Inspection recorded. Granting a badge is a separate decision below.');
          this.saving.set(false);
          this.loadDue();
        },
        error: (err) => this.fail(err),
      });
  }

  badge(action: 'grant' | 'remove'): void {
    const id = this.badgePropertyId() ?? 0;
    if (!Number.isInteger(id) || id <= 0) {
      this.error.set('Choose a listing.');
      return;
    }
    const problem = requiredNote(this.badgeReason());
    if (problem) {
      this.error.set(problem);
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    this.notice.set(null);
    this.api.changeBadge(id, action, this.badgeReason().trim()).subscribe({
      next: () => {
        this.notice.set(action === 'grant' ? 'Inspection badge granted.' : 'Inspection badge removed.');
        this.badgeReason.set('');
        this.saving.set(false);
        this.loadDue();
      },
      error: (err) => this.fail(err),
    });
  }

  private loadPermission(): void {
    const id = this.auth.currentUser()?.id;
    if (!id) return;
    this.api.user(id).subscribe({
      next: (user) => this.canInspect.set(user.canInspect === true),
      error: (err) => this.error.set(toFailure(err).message),
    });
  }

  private fail(err: unknown): void {
    this.error.set(toFailure(err).message);
    this.saving.set(false);
  }
}
