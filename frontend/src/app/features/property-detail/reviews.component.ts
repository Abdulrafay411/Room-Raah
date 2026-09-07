import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { PropertyService } from '../../core/services/property.service';
import { toFailure } from '../../core/http/api-error';
import { Review, StayStatus } from '../../core/models/catalog.model';

const STAY_LABELS: Record<StayStatus, string> = {
  Visited: 'Visited',
  CurrentlyStaying: 'Currently staying',
  PreviouslyStayed: 'Previously stayed',
};

/**
 * Every approved review for one listing.
 *
 * Only approved ones exist as far as this page is concerned - the API does not send the
 * rest, which is rule 4. Somebody who has just written one will not find it here, and the
 * screen that took it says so at the time rather than leaving them to wonder.
 */
@Component({
  selector: 'app-property-reviews',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './reviews.component.html',
  styleUrls: ['./reviews.component.scss'],
})
export class PropertyReviewsComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly properties = inject(PropertyService);

  readonly reviews = signal<Review[]>([]);
  readonly average = signal(0);
  readonly total = signal(0);
  readonly page = signal(1);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly propertyId = signal(0);

  readonly pageSize = 12;
  readonly lastPage = computed(() => Math.max(1, Math.ceil(this.total() / this.pageSize)));

  constructor() {
    this.route.paramMap.subscribe((params) => {
      this.propertyId.set(Number(params.get('id')));
      this.load(1);
    });
  }

  load(page: number): void {
    if (page < 1) return;
    this.loading.set(true);
    this.error.set(null);

    this.properties.reviews(this.propertyId(), page, this.pageSize).subscribe({
      next: (result) => {
        this.reviews.set(result.items ?? []);
        this.average.set(result.averageRating ?? 0);
        this.total.set(result.totalCount ?? 0);
        this.page.set(page);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(toFailure(err).message);
        this.loading.set(false);
      },
    });
  }

  stayLabel(status: StayStatus): string {
    return STAY_LABELS[status] ?? status;
  }
}
