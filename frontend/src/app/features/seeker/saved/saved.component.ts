import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { EmptyStateComponent, PropertyCardComponent } from '../../../shared/components';
import { EngagementService } from '../../../core/services/engagement.service';
import { toFailure } from '../../../core/http/api-error';
import { SavedListing } from '../../../core/models/engagement.model';

/**
 * The shortlist. Page 14.
 *
 * A listing that leaves the site drops out of this list while its row survives, so the count
 * can be smaller than the number of things somebody saved and that is correct rather than a
 * failed delete. If the listing comes back, so does the card.
 */
@Component({
  selector: 'app-saved',
  standalone: true,
  imports: [CommonModule, RouterLink, PropertyCardComponent, EmptyStateComponent],
  templateUrl: './saved.component.html',
  styleUrls: ['./saved.component.scss'],
})
export class SavedComponent {
  private readonly engagement = inject(EngagementService);

  readonly items = signal<SavedListing[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly skeletons = Array.from({ length: 3 });

  constructor() {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.engagement.saved(1, 50).subscribe({
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

  /**
   * Removing from this page takes the card away immediately rather than waiting for a
   * re-fetch: the person just pressed the button, and a row that lingers reads as a failure.
   */
  remove(propertyId: number): void {
    this.items.update((list) => list.filter((item) => item.id !== propertyId));
    this.engagement.unsave(propertyId).subscribe({ error: () => this.load() });
  }

  /** Up to three at a time; the fourth id is a 422 the server would refuse. */
  compareLink(): (string | number)[] {
    return ['/compare'];
  }

  compareIds(): string {
    return this.items()
      .slice(0, 3)
      .map((item) => item.id)
      .join(',');
  }
}
