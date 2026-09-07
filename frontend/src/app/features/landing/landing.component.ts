import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { PropertyCardComponent } from '../../shared/components';
import { PropertyService } from '../../core/services/property.service';
import { ReferenceService } from '../../core/services/reference.service';
import { EngagementService } from '../../core/services/engagement.service';
import { LocationSuggestion, PropertyCard } from '../../core/models/catalog.model';

/**
 * The front page: say what this is, take one search, and show a few real listings.
 *
 * Featured listings load anonymously and are not gated behind anything - browsing is
 * public, and a landing page that asks for an account before showing a single room is a
 * landing page people leave.
 */
@Component({
  selector: 'app-landing',
  standalone: true,
  imports: [CommonModule, RouterLink, PropertyCardComponent],
  templateUrl: './landing.component.html',
  styleUrls: ['./landing.component.scss'],
})
export class LandingComponent {
  private readonly properties = inject(PropertyService);
  private readonly reference = inject(ReferenceService);
  private readonly router = inject(Router);
  private readonly engagement = inject(EngagementService);

  readonly featured = signal<PropertyCard[]>([]);
  readonly loading = signal(true);

  readonly query = signal('');
  readonly suggestions = signal<LocationSuggestion[]>([]);

  readonly skeletons = Array.from({ length: 3 });

  constructor() {
    this.engagement.primeShortlist();

    this.properties.featured().subscribe({
      next: (list) => {
        this.featured.set((list ?? []).slice(0, 6));
        this.loading.set(false);
      },
      // A landing page with no featured strip is a smaller page, not a broken one. There
      // is nothing here a person can act on that failing to load would leave misleading.
      error: () => {
        this.featured.set([]);
        this.loading.set(false);
      },
    });
  }

  isSaved(id: number): boolean {
    return this.engagement.isSaved(id);
  }

  toggleSave(id: number): void {
    this.engagement.toggle(id, '/');
  }

  onQuery(value: string): void {
    this.query.set(value);
    this.reference.suggest(value).subscribe((list) => this.suggestions.set(list));
  }

  choose(suggestion: LocationSuggestion): void {
    this.suggestions.set([]);

    const queryParams =
      suggestion.type === 'Landmark'
        ? { landmarkId: suggestion.id }
        : suggestion.type === 'City'
          ? { cityId: suggestion.id }
          : { areaId: suggestion.id };

    this.router.navigate(['/search'], { queryParams });
  }

  /** Enter with nothing chosen still gets somewhere useful rather than doing nothing. */
  submit(): void {
    const first = this.suggestions()[0];
    if (first) {
      this.choose(first);
      return;
    }
    this.router.navigate(['/search']);
  }
}
