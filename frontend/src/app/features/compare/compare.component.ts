import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { map } from 'rxjs/operators';
import { BadgeComponent, EmptyStateComponent } from '../../shared/components';
import { COMPARE_LIMIT, PropertyService } from '../../core/services/property.service';
import { toFailure } from '../../core/http/api-error';
import { PropertyCard } from '../../core/models/catalog.model';
import { GeolocationService } from '../../core/services/geolocation.service';

/**
 * Page 13: up to three listings beside each other.
 *
 * The ids live in the query string for the same reason the search filters do - a comparison
 * is a thing people send each other, and it has to survive being pasted. A fourth id is a
 * 422, so the list is capped here rather than allowed to fail out at the server; an id that
 * names nothing is dropped quietly by the API rather than refusing the whole request, which
 * is why the count on screen can be smaller than the count in the URL.
 */
@Component({
  selector: 'app-compare',
  standalone: true,
  imports: [CommonModule, RouterLink, BadgeComponent, EmptyStateComponent],
  templateUrl: './compare.component.html',
  styleUrls: ['./compare.component.scss'],
})
export class CompareComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly properties = inject(PropertyService);
  private readonly geo = inject(GeolocationService);

  readonly limit = COMPARE_LIMIT;

  readonly listings = signal<PropertyCard[]>([]);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  /** Carried in the query string from the search that produced the shortlist. */
  readonly landmarkId = signal<number | undefined>(undefined);

  private readonly ids = toSignal(
    this.route.queryParams.pipe(map((params) => readIds(params['ids']))),
    { initialValue: [] as number[] },
  );

  readonly hasIds = computed(() => this.ids().length > 0);

  /** The rows are fixed rather than derived, so an absent value shows as a gap in a table. */
  readonly rows: ReadonlyArray<{ label: string; get: (item: PropertyCard) => string }> = [
    { label: 'Monthly rent', get: (item) => money(item.monthlyRent) },
    { label: 'Security deposit', get: (item) => money(item.securityDeposit) },
    { label: 'Utilities', get: (item) => money(item.utilitiesCharge) },
    { label: 'Mess', get: (item) => money(item.messCharge) },
    {
      label: 'Monthly total',
      get: (item) => money(item.monthlyRent + item.utilitiesCharge + item.messCharge),
    },
    { label: 'Room type', get: (item) => item.roomType },
    { label: 'Who it is for', get: (item) => item.genderPolicy },
    { label: 'Beds free', get: (item) => `${item.availableBeds} of ${item.totalBeds}` },
    { label: 'Where', get: (item) => `${item.areaName}, ${item.cityName}` },
    {
      label: 'Distance from you',
      // Rule 13: straight-line, and labelled as such. Worked out here from the person's own
      // position, because the server measures only from a landmark. A dash is 'we do not
      // know where you are', which is a question, not a zero.
      get: (item) => {
        const km = this.geo.distanceTo(item.latitude, item.longitude);
        return km === null ? '—' : `${km.toFixed(1)} km (straight-line)`;
      },
    },
    { label: 'Inspected', get: (item) => (item.hasInspectionBadge ? 'Yes' : 'Not yet') },
  ];

  constructor() {
    this.route.queryParams.subscribe((params) => {
      const landmark = Number(params['landmarkId']);
      this.landmarkId.set(Number.isFinite(landmark) && landmark > 0 ? landmark : undefined);
      this.load(readIds(params['ids']));
    });
  }

  private load(ids: number[]): void {
    if (ids.length === 0) {
      this.listings.set([]);
      this.error.set(null);
      return;
    }

    this.loading.set(true);
    this.error.set(null);

    this.properties.compare(ids, this.landmarkId()).subscribe({
      next: (list) => {
        this.listings.set(list ?? []);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(toFailure(err).message);
        this.loading.set(false);
      },
    });
  }

  remove(id: number): void {
    const next = this.ids().filter((value) => value !== id);
    this.router.navigate(['/compare'], {
      queryParams: { ids: next.length ? next.join(',') : null, landmarkId: this.landmarkId() ?? null },
    });
  }
}

/** `?ids=1,2,3`. Anything that is not a positive number is dropped rather than sent. */
export function readIds(raw: unknown): number[] {
  if (typeof raw !== 'string' || !raw.trim()) return [];
  const ids = raw
    .split(',')
    .map((part) => Number(part.trim()))
    .filter((value) => Number.isInteger(value) && value > 0);
  return [...new Set(ids)].slice(0, COMPARE_LIMIT);
}

function money(value: number): string {
  return new Intl.NumberFormat('en-PK', {
    style: 'currency',
    currency: 'PKR',
    maximumFractionDigits: 0,
  }).format(value);
}
