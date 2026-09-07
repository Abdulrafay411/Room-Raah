import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { BadgeComponent, ButtonComponent } from '../../shared/components';
import { AuthService } from '../../core/auth/auth.service';
import { toFailure } from '../../core/http/api-error';

/**
 * Page 17. Who you are signed in as, and the three things you can do about it.
 *
 * There is no `GET /auth/me` in this API and no endpoint that edits an account, so this
 * page shows what the session already holds rather than inventing a profile call. That is
 * the honest version: a screen full of fields that cannot be saved is worse than a short
 * screen that can.
 *
 * Changing a password goes through the same forgot-password flow as anywhere else -
 * proving control of the address is the point of it, and there is no endpoint that takes
 * an old password and a new one.
 */
@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [CommonModule, RouterLink, BadgeComponent, ButtonComponent],
  templateUrl: './profile.component.html',
  styleUrls: ['./profile.component.scss'],
})
export class ProfileComponent {
  readonly auth = inject(AuthService);

  readonly sending = signal(false);
  readonly notice = signal<string | null>(null);
  readonly error = signal<string | null>(null);

  readonly user = this.auth.currentUser;
  readonly role = this.auth.userRole;
  readonly verified = this.auth.isEmailVerified;

  readonly roleLabel = computed(() => {
    switch (this.role()) {
      case 'Owner':
        return 'You list rooms on RoomRaah.';
      case 'Admin':
        return 'You review listings, revisions and reports.';
      case 'Seeker':
        return 'You are looking for a room.';
      default:
        return '';
    }
  });

  /**
   * Sends the reset link to the address on the account. The server answers the same way
   * whether or not an address exists, so this never confirms or denies one - and the copy
   * has to match that, or it leaks by implication.
   */
  changePassword(): void {
    const email = this.user()?.email;
    if (!email) return;

    this.sending.set(true);
    this.notice.set(null);
    this.error.set(null);

    this.auth.forgotPassword({ email }).subscribe({
      next: () => {
        this.notice.set(
          'If that address has an account, a reset link is on its way. Open it, then set a new password.',
        );
        this.sending.set(false);
      },
      error: (err) => {
        this.error.set(toFailure(err).message);
        this.sending.set(false);
      },
    });
  }

  signOut(): void {
    this.auth.logout().subscribe();
  }
}
