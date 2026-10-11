import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ADMIN_PAGE_SIZE, AdminService } from '../../../core/services/admin.service';
import { AuthService } from '../../../core/auth/auth.service';
import { DueInspection, inspectionProblem, requiredNote } from '../../../core/models/admin.model';
import { toLocalInputValue } from '../../../core/models/engagement.model';
import { toFailure } from '../../../core/http/api-error';
import { ButtonComponent } from '../../../shared/components';
import {
  AdminPagerComponent,
  AdminWorkspaceComponent,
  ConfirmDialogComponent,
  ConfirmRequest,
} from '../shared';

/** The listing the two forms act on. Chosen from the worklist, or looked up by number. */
interface Target {
  id: number;
  title: string;
  where: string;
  owner: string;
  lastInspectionAt: string | null;
  hasBadge: boolean | null;
}

@Component({
  selector: 'app-admin-inspections',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ButtonComponent,
    AdminWorkspaceComponent,
    AdminPagerComponent,
    ConfirmDialogComponent,
  ],
  templateUrl: './admin-inspections.component.html',
  styleUrls: ['./admin-inspections.component.scss'],
})
export class AdminInspectionsComponent {
  private readonly api = inject(AdminService);
  private readonly auth = inject(AuthService);

  readonly due = signal<DueInspection[]>([]);
  readonly page = signal(1);
  readonly total = signal(0);
  readonly pageSize = ADMIN_PAGE_SIZE;

  readonly canInspect = signal(false);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);
  readonly confirm = signal<ConfirmRequest | null>(null);

  readonly target = signal<Target | null>(null);
  readonly lookingUp = signal(false);
  /** On a phone the forms live in a drawer; this opens it without a worklist row. */
  readonly drawerForced = signal(false);
  readonly detailOpen = computed(() => !!this.target() || this.drawerForced());
  readonly lookupId = signal<number | null>(null);

  readonly inspectedAt = signal(toLocalInputValue(new Date()));
  readonly result = signal<'Passed' | 'Failed'>('Passed');
  readonly notes = signal('');
  readonly feeAmount = signal<number | null>(null);
  readonly feeCollectedAt = signal('');

  readonly badgeReason = signal('');
  readonly maxNow = toLocalInputValue(new Date());

  constructor() {
    this.loadPermission();
    this.loadDue();
  }

  goTo(page: number): void {
    this.page.set(page);
    this.loadDue();
  }

  loadDue(): void {
    this.loading.set(true);
    this.api.dueInspections(this.page()).subscribe({
      next: (page) => {
        this.due.set(page.items ?? []);
        this.total.set(page.totalCount ?? page.items?.length ?? 0);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(toFailure(err).message);
        this.loading.set(false);
      },
    });
  }

  use(row: DueInspection): void {
    this.error.set(null);
    this.notice.set(null);
    this.target.set({
      id: row.propertyId,
      title: row.title,
      where: `${row.areaName}, ${row.cityName}`,
      owner: row.ownerDisplayName,
      lastInspectionAt: row.lastInspectionAt,
      hasBadge: null,
    });
    // The worklist does not say whether the listing already carries the badge; the
    // listing itself does, and it decides which badge button makes sense.
    this.api.property(row.propertyId).subscribe({
      next: (detail) =>
        this.target.update((current) =>
          current?.id === detail.id ? { ...current, hasBadge: detail.hasInspectionBadge } : current,
        ),
      error: () => undefined,
    });
  }

  /** For a listing that is not on the worklist - one with a badge to remove, say. */
  lookUp(): void {
    const id = this.lookupId() ?? 0;
    if (!Number.isInteger(id) || id <= 0) {
      this.error.set('Enter a listing number.');
      return;
    }
    this.lookingUp.set(true);
    this.error.set(null);
    this.api.property(id).subscribe({
      next: (detail) => {
        this.target.set({
          id: detail.id,
          title: detail.title,
          where: `${detail.areaName}, ${detail.cityName}`,
          owner: detail.owner.fullName,
          lastInspectionAt: null,
          hasBadge: detail.hasInspectionBadge,
        });
        this.lookupId.set(null);
        this.lookingUp.set(false);
      },
      error: (err) => {
        this.error.set(toFailure(err).message);
        this.lookingUp.set(false);
      },
    });
  }

  openDrawer(): void {
    this.drawerForced.set(true);
  }

  clearTarget(): void {
    this.target.set(null);
  }

  close(): void {
    this.target.set(null);
    this.drawerForced.set(false);
    this.confirm.set(null);
  }

  record(): void {
    const id = this.target()?.id ?? 0;
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
        feeCollectedAt: this.feeCollectedAt() ? new Date(this.feeCollectedAt()).toISOString() : null,
      })
      .subscribe({
        next: (saved) => {
          this.notice.set(
            saved.result === 'Passed'
              ? 'Inspection recorded as passed. Granting the badge is the separate step below.'
              : 'Inspection recorded as failed.',
          );
          this.notes.set('');
          this.feeAmount.set(null);
          this.feeCollectedAt.set('');
          this.target.update((current) =>
            current ? { ...current, lastInspectionAt: saved.inspectedAt } : current,
          );
          this.saving.set(false);
          this.loadDue();
        },
        error: (err) => this.fail(err),
      });
  }

  /** Granting adds something; removing takes it away, so removing asks first. */
  requestBadge(action: 'grant' | 'remove'): void {
    const target = this.target();
    if (!target) {
      this.error.set('Choose a listing first.');
      return;
    }
    const problem = requiredNote(this.badgeReason());
    if (problem) {
      this.error.set(problem);
      return;
    }
    this.error.set(null);
    if (action === 'grant') {
      this.badge('grant');
      return;
    }
    this.confirm.set({
      title: 'Remove the inspection badge?',
      description: `"${target.title}" loses the RoomRaah inspection badge on every seeker-facing screen until an inspector grants it again.`,
      quote: this.badgeReason(),
      confirmLabel: 'Remove badge',
      run: () => this.badge('remove'),
    });
  }

  private badge(action: 'grant' | 'remove'): void {
    const target = this.target();
    if (!target) return;

    this.saving.set(true);
    this.error.set(null);
    this.notice.set(null);
    this.api.changeBadge(target.id, action, this.badgeReason().trim()).subscribe({
      next: (updated) => {
        this.confirm.set(null);
        this.notice.set(action === 'grant' ? 'Inspection badge granted.' : 'Inspection badge removed.');
        this.badgeReason.set('');
        this.target.update((current) =>
          current ? { ...current, hasBadge: updated.hasInspectionBadge } : current,
        );
        this.saving.set(false);
        this.loadDue();
      },
      error: (err) => {
        this.confirm.set(null);
        this.fail(err);
      },
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
