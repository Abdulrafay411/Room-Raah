import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../../../core/services/admin.service';
import { AuthService } from '../../../core/auth/auth.service';
import { AdminUserDetail, AdminUserSummary, requiredNote } from '../../../core/models/admin.model';
import { toFailure } from '../../../core/http/api-error';
import { ButtonComponent } from '../../../shared/components';

@Component({
  selector: 'app-admin-users',
  standalone: true,
  imports: [CommonModule, FormsModule, ButtonComponent],
  templateUrl: './admin-users.component.html',
  styleUrls: ['./admin-users.component.scss'],
})
export class AdminUsersComponent {
  private readonly api = inject(AdminService);
  private readonly auth = inject(AuthService);

  readonly items = signal<AdminUserSummary[]>([]);
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

  constructor() {
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
    this.api.users({ role: this.role(), isActive: this.active(), q }).subscribe({
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

  clear(): void {
    this.role.set('');
    this.active.set('');
    this.query.set('');
    this.load();
  }

  open(row: AdminUserSummary): void {
    this.detailLoading.set(true);
    this.error.set(null);
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

  canChange(user: AdminUserDetail): boolean {
    return user.role !== 'Admin' && user.id !== this.auth.currentUser()?.id;
  }

  changeActive(active: boolean): void {
    const user = this.selected();
    if (!user || !this.canChange(user)) return;
    const problem = requiredNote(this.reason());
    if (problem) {
      this.error.set(problem);
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    this.notice.set(null);
    this.api.setUserActive(user.id, active, this.reason().trim()).subscribe({
      next: (updated) => {
        this.selected.set({ ...user, ...updated });
        this.reason.set('');
        this.notice.set(active ? 'Account reactivated.' : 'Account deactivated. Their public listings were updated automatically.');
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
