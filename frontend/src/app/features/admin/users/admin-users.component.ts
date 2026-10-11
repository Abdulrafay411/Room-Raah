import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ADMIN_PAGE_SIZE, AdminService } from '../../../core/services/admin.service';
import { AuthService } from '../../../core/auth/auth.service';
import { AdminUserDetail, AdminUserSummary, requiredNote } from '../../../core/models/admin.model';
import { toFailure } from '../../../core/http/api-error';
import { ButtonComponent } from '../../../shared/components';
import {
  AdminPagerComponent,
  AdminStatusComponent,
  AdminWorkspaceComponent,
  ConfirmDialogComponent,
  ConfirmRequest,
} from '../shared';

@Component({
  selector: 'app-admin-users',
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
  templateUrl: './admin-users.component.html',
  styleUrls: ['./admin-users.component.scss'],
})
export class AdminUsersComponent {
  private readonly api = inject(AdminService);
  private readonly auth = inject(AuthService);

  readonly items = signal<AdminUserSummary[]>([]);
  readonly page = signal(1);
  readonly total = signal(0);
  readonly pageSize = ADMIN_PAGE_SIZE;

  readonly selected = signal<AdminUserDetail | null>(null);
  readonly role = signal('');
  readonly active = signal('');
  readonly query = signal('');
  readonly reason = signal('');
  readonly loading = signal(true);
  readonly detailLoading = signal(false);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);
  readonly confirm = signal<ConfirmRequest | null>(null);

  constructor() {
    this.load();
  }

  search(): void {
    this.page.set(1);
    this.load();
  }

  goTo(page: number): void {
    this.page.set(page);
    this.load();
  }

  load(): void {
    const q = this.query().trim();
    if (q.length === 1) {
      this.error.set('Search needs at least 2 characters.');
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    this.api.users({ role: this.role(), isActive: this.active(), q }, this.page()).subscribe({
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

  clear(): void {
    this.role.set('');
    this.active.set('');
    this.query.set('');
    this.page.set(1);
    this.load();
  }

  open(row: AdminUserSummary): void {
    this.detailLoading.set(true);
    this.error.set(null);
    this.notice.set(null);
    this.reason.set('');
    this.api.user(row.id).subscribe({
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

  canChange(user: AdminUserDetail): boolean {
    return user.role !== 'Admin' && user.id !== this.auth.currentUser()?.id;
  }

  /** Reactivating gives something back. Deactivating takes it away, so it asks first. */
  request(active: boolean): void {
    const user = this.selected();
    if (!user || !this.canChange(user)) return;
    const problem = requiredNote(this.reason());
    if (problem) {
      this.error.set(problem);
      return;
    }
    this.error.set(null);
    if (active) {
      this.changeActive(true);
      return;
    }
    this.confirm.set({
      title: 'Deactivate this account?',
      description:
        user.role === 'Owner'
          ? `${user.fullName} can no longer sign in, and every listing they own comes out of search straight away.`
          : `${user.fullName} can no longer sign in. Their saved rooms, visits and messages stay as they are.`,
      quote: this.reason(),
      confirmLabel: 'Deactivate account',
      run: () => this.changeActive(false),
    });
  }

  private changeActive(active: boolean): void {
    const user = this.selected();
    if (!user) return;

    this.saving.set(true);
    this.error.set(null);
    this.notice.set(null);
    this.api.setUserActive(user.id, active, this.reason().trim()).subscribe({
      next: (updated) => {
        // Rule 137: the summary that comes back does not carry the reason, but this
        // screen just sent it, so the detail can show it without another request.
        const reason = this.reason().trim();
        this.selected.set({ ...user, ...updated, deactivationReason: active ? null : reason });
        this.reason.set('');
        this.confirm.set(null);
        this.notice.set(
          active
            ? 'Account reactivated. They can sign in again.'
            : 'Account deactivated.' + (user.role === 'Owner' ? ' Their listings are out of search.' : ''),
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
