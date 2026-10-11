import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ADMIN_PAGE_SIZE, AdminService } from '../../../core/services/admin.service';
import { RevisionDetail, RevisionKind, RevisionQueueRow, requiredNote } from '../../../core/models/admin.model';
import { toFailure } from '../../../core/http/api-error';
import { ButtonComponent } from '../../../shared/components';
import {
  AdminPagerComponent,
  AdminStatusComponent,
  AdminWorkspaceComponent,
  ConfirmDialogComponent,
  ConfirmRequest,
  fieldLabel,
} from '../shared';

@Component({
  selector: 'app-admin-revisions',
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
  templateUrl: './admin-revisions.component.html',
  styleUrls: ['./admin-revisions.component.scss'],
})
export class AdminRevisionsComponent {
  private readonly api = inject(AdminService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly kind = signal<RevisionKind>('Content');
  readonly items = signal<RevisionQueueRow[]>([]);
  readonly page = signal(1);
  readonly total = signal(0);
  readonly pageSize = ADMIN_PAGE_SIZE;

  readonly selected = signal<RevisionDetail | null>(null);
  readonly detailLoading = signal(false);
  readonly note = signal('');
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);
  readonly confirm = signal<ConfirmRequest | null>(null);

  readonly fieldLabel = fieldLabel;

  constructor() {
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const next: RevisionKind = params.get('kind') === 'Availability' ? 'Availability' : 'Content';
      this.kind.set(next);
      this.page.set(1);
      this.selected.set(null);
      this.load();
    });
  }

  choose(kind: RevisionKind): void {
    this.router.navigate([], { relativeTo: this.route, queryParams: { kind } });
  }

  goTo(page: number): void {
    this.page.set(page);
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.revisions(this.kind(), this.page()).subscribe({
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

  open(row: RevisionQueueRow): void {
    this.detailLoading.set(true);
    this.error.set(null);
    this.notice.set(null);
    this.note.set('');
    this.api.revision(row.id).subscribe({
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
    this.confirm.set(null);
  }

  /** Approving is quick and reversible by a later revision. Rejecting asks first. */
  request(revision: { id: number; propertyTitle: string }, action: 'approve' | 'reject'): void {
    if (action === 'approve') {
      this.decide(revision.id, 'approve');
      return;
    }
    const problem = requiredNote(this.note());
    if (problem) {
      this.error.set(problem);
      return;
    }
    this.error.set(null);
    this.confirm.set({
      title: 'Reject this revision?',
      description: `The live listing for "${revision.propertyTitle}" stays as it is. The owner is told why their change was not accepted.`,
      quote: this.note(),
      confirmLabel: 'Reject revision',
      run: () => this.decide(revision.id, 'reject'),
    });
  }

  private decide(id: number, action: 'approve' | 'reject'): void {
    this.saving.set(true);
    this.error.set(null);
    this.notice.set(null);
    this.api.decideRevision(id, action, this.note()).subscribe({
      next: (updated) => {
        if (this.selected()?.id === id) this.selected.set(updated);
        this.note.set('');
        this.confirm.set(null);
        this.notice.set(
          action === 'approve'
            ? 'Revision approved. The live listing now shows the change.'
            : 'Revision rejected. The owner has your note.',
        );
        this.saving.set(false);
        this.load();
      },
      error: (err) => {
        this.confirm.set(null);
        this.error.set(toFailure(err).message);
        this.saving.set(false);
      },
    });
  }
}
