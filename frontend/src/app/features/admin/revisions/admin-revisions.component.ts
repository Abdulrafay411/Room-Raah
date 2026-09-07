import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AdminService } from '../../../core/services/admin.service';
import { RevisionDetail, RevisionKind, RevisionQueueRow, requiredNote } from '../../../core/models/admin.model';
import { toFailure } from '../../../core/http/api-error';
import { ButtonComponent } from '../../../shared/components';

@Component({
  selector: 'app-admin-revisions',
  standalone: true,
  imports: [CommonModule, FormsModule, ButtonComponent],
  templateUrl: './admin-revisions.component.html',
  styleUrls: ['./admin-revisions.component.scss'],
})
export class AdminRevisionsComponent {
  private readonly api = inject(AdminService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly kind = signal<RevisionKind>('Content');
  readonly items = signal<RevisionQueueRow[]>([]);
  readonly selected = signal<RevisionDetail | null>(null);
  readonly note = signal('');
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);

  constructor() {
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const next: RevisionKind = params.get('kind') === 'Availability' ? 'Availability' : 'Content';
      this.kind.set(next);
      this.selected.set(null);
      this.load();
    });
  }

  choose(kind: RevisionKind): void {
    this.router.navigate([], { relativeTo: this.route, queryParams: { kind } });
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.revisions(this.kind()).subscribe({
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

  open(row: RevisionQueueRow): void {
    this.error.set(null);
    this.api.revision(row.id).subscribe({
      next: (detail) => this.selected.set(detail),
      error: (err) => this.error.set(toFailure(err).message),
    });
  }

  decide(id: number, action: 'approve' | 'reject'): void {
    if (action === 'reject') {
      const problem = requiredNote(this.note());
      if (problem) {
        this.error.set(problem);
        return;
      }
    }

    this.saving.set(true);
    this.error.set(null);
    this.notice.set(null);
    this.api.decideRevision(id, action, this.note()).subscribe({
      next: (updated) => {
        this.selected.set(updated);
        this.note.set('');
        this.notice.set(action === 'approve' ? 'Revision approved.' : 'Revision rejected.');
        this.saving.set(false);
        this.load();
      },
      error: (err) => {
        this.error.set(toFailure(err).message);
        this.saving.set(false);
      },
    });
  }
}
