import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Subject, catchError, debounceTime, distinctUntilChanged, of, switchMap } from 'rxjs';
import { ADMIN_PAGE_SIZE, AdminService } from '../../../core/services/admin.service';
import { AuthService } from '../../../core/auth/auth.service';
import {
  AdminPropertySummary,
  DueInspection,
  RequestedInspection,
  inspectionProblem,
  isListingSearch,
  listingNumber,
  requiredNote,
} from '../../../core/models/admin.model';
import { toLocalInputValue } from '../../../core/models/engagement.model';
import { toFailure } from '../../../core/http/api-error';
import { ButtonComponent } from '../../../shared/components';
import {
  AdminPagerComponent,
  AdminWorkspaceComponent,
  ConfirmDialogComponent,
  ConfirmRequest,
} from '../shared';

/** The listing the two forms act on. Chosen from a worklist, or found by search or number. */
interface Target {
  id: number;
  title: string;
  where: string;
  owner: string;
  lastInspectionAt: string | null;
  hasBadge: boolean | null;
}

/**
 * Two worklists: owners waiting for an inspection they asked for - rule 136 - and badged
 * listings due for a re-inspection - rule 114. A listing on neither is found by name,
 * city, owner or number (rule 135).
 */
type Worklist = 'requested' | 'due';

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

  readonly worklist = signal<Worklist>('requested');
  readonly requested = signal<RequestedInspection[]>([]);
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

  /** What the admin typed to find a listing: a name, a city, an owner or an ID. */
  readonly query = signal('');
  readonly results = signal<AdminPropertySummary[]>([]);
  readonly searching = signal(false);
  private readonly terms = new Subject<string>();

  readonly inspectedAt = signal(toLocalInputValue(new Date()));
  readonly result = signal<'Passed' | 'Failed'>('Passed');
  readonly notes = signal('');
  readonly feeAmount = signal<number | null>(null);
  readonly feeCollectedAt = signal('');

  readonly badgeReason = signal('');
  readonly maxNow = toLocalInputValue(new Date());

  constructor() {
    this.loadPermission();
    this.load();

    // Published only: an inspection and a badge both belong to a live listing.
    this.terms
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        switchMap((term) => {
          if (!isListingSearch(term)) return of(null);
          this.searching.set(true);
          return this.api.properties('Published', 1, term.trim()).pipe(catchError(() => of(null)));
        }),
        takeUntilDestroyed(),
      )
      .subscribe((page) => {
        this.searching.set(false);
        this.results.set(page?.items?.slice(0, 8) ?? []);
      });
  }

  // --- worklists -----------------------------------------------------------------------

  show(list: Worklist): void {
    if (this.worklist() === list) return;
    this.worklist.set(list);
    this.page.set(1);
    this.load();
  }

  goTo(page: number): void {
    this.page.set(page);
    this.load();
  }

  load(): void {
    this.loading.set(true);
    const done = (total: number) => {
      this.total.set(total);
      this.loading.set(false);
    };
    const failed = (err: unknown) => {
      this.error.set(toFailure(err).message);
      this.loading.set(false);
    };

    if (this.worklist() === 'requested') {
      this.api.requestedInspections(this.page()).subscribe({
        next: (page) => {
          this.requested.set(page.items ?? []);
          done(page.totalCount ?? page.items?.length ?? 0);
        },
        error: failed,
      });
    } else {
      this.api.dueInspections(this.page()).subscribe({
        next: (page) => {
          this.due.set(page.items ?? []);
          done(page.totalCount ?? page.items?.length ?? 0);
        },
        error: failed,
      });
    }
  }

  use(row: DueInspection): void {
    this.choose({
      id: row.propertyId,
      title: row.title,
      where: `${row.areaName}, ${row.cityName}`,
      owner: row.ownerDisplayName,
      lastInspectionAt: row.lastInspectionAt,
      hasBadge: null,
    });
    // The due worklist does not say whether the listing still carries the badge; the
    // listing itself does, and it decides which badge button makes sense.
    this.api.property(row.propertyId).subscribe({
      next: (detail) =>
        this.target.update((current) =>
          current?.id === detail.id ? { ...current, hasBadge: detail.hasInspectionBadge } : current,
        ),
      error: () => undefined,
    });
  }

  useRequested(row: RequestedInspection): void {
    this.choose({
      id: row.propertyId,
      title: row.title,
      where: `${row.areaName}, ${row.cityName}`,
      owner: row.ownerDisplayName,
      lastInspectionAt: row.lastInspectionAt,
      hasBadge: row.hasInspectionBadge,
    });
  }

  // --- finding a listing (rule 135) ----------------------------------------------------

  onQuery(text: string): void {
    this.query.set(text);
    if (!isListingSearch(text)) this.results.set([]);
    this.terms.next(text);
  }

  pick(row: AdminPropertySummary): void {
    this.choose({
      id: row.id,
      title: row.title,
      where: `${row.areaName}, ${row.cityName}`,
      owner: row.ownerName,
      lastInspectionAt: null,
      hasBadge: row.hasInspectionBadge,
    });
  }

  /**
   * Enter in the search box: a number opens that listing, a search with results takes the
   * first one, and anything else says why nothing happened.
   */
  lookUp(): void {
    const text = this.query();
    const id = listingNumber(text);

    if (id === null) {
      const first = this.results()[0];
      if (first) {
        this.pick(first);
      } else {
        this.error.set(
          isListingSearch(text)
            ? 'No published listing matches that name, city or owner.'
            : 'Type at least two letters of a name, city or owner, or a listing ID.',
        );
      }
      return;
    }

    this.lookingUp.set(true);
    this.error.set(null);
    this.api.property(id).subscribe({
      next: (detail) => {
        this.choose({
          id: detail.id,
          title: detail.title,
          where: `${detail.areaName}, ${detail.cityName}`,
          owner: detail.owner.fullName,
          lastInspectionAt: null,
          hasBadge: detail.hasInspectionBadge,
        });
        this.lookingUp.set(false);
      },
      error: (err) => {
        this.error.set(toFailure(err).message);
        this.lookingUp.set(false);
      },
    });
  }

  private choose(target: Target): void {
    this.error.set(null);
    this.notice.set(null);
    this.target.set(target);
    this.query.set('');
    this.results.set([]);
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

  // --- inspection and badge ------------------------------------------------------------

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
          this.load();
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
      description: `"${target.title}" loses the RoomRaah inspection badge on every seeker-facing screen until an inspector grants it again. The owner is shown your reason.`,
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
        this.load();
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
