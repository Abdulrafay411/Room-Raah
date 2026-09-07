import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Params, Router, RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { map } from 'rxjs/operators';
import {
  BadgeComponent,
  ButtonComponent,
  EmptyStateComponent,
  PropertyCardComponent,
} from '../../shared/components';
import { PropertyService } from '../../core/services/property.service';
import { ReferenceService } from '../../core/services/reference.service';
import { EngagementService } from '../../core/services/engagement.service';
import { CompareStore } from '../../core/services/compare.store';
import { toFailure } from '../../core/http/api-error';
import {
  Area,
  City,
  Facility,
  GENDER_POLICIES,
  GenderPolicy,
  LocationSuggestion,
  PAGE_SIZE_DEFAULT,
  PropertyCard,
  PropertyFilters,
  PropertySort,
  ROOM_TYPES,
  RoomType,
  SORT_OPTIONS,
  filterProblem,
} from '../../core/models/catalog.model';

/**
 * Search, and the filter panel that drives it.
 *
 * The filters live in the query string rather than in component state, which is why page 08
 * of the brief has no route of its own: a filtered search is then a URL somebody can send,
 * and the back button walks back through filter changes the way people expect it to. The
 * component reads the URL and renders it; changing a filter navigates, and the navigation
 * is what triggers the fetch. There is deliberately no second copy of the filter state.
 */
@Component({
  selector: 'app-search',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    PropertyCardComponent,
    ButtonComponent,
    BadgeComponent,
    EmptyStateComponent,
    RouterLink,
  ],
  templateUrl: './search.component.html',
  styleUrls: ['./search.component.scss'],
})
export class SearchComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly properties = inject(PropertyService);
  private readonly reference = inject(ReferenceService);
  private readonly engagement = inject(EngagementService);
  /** Public: the template ticks rooms and reads the count straight off it. */
  readonly compare = inject(CompareStore);

  readonly roomTypes = ROOM_TYPES;
  readonly genderPolicies = GENDER_POLICIES;
  readonly sortOptions = SORT_OPTIONS;

  readonly results = signal<PropertyCard[]>([]);
  readonly total = signal(0);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  readonly cities = signal<City[]>([]);
  readonly areas = signal<Area[]>([]);
  readonly facilities = signal<Facility[]>([]);

  /** Phone shows the panel as a slide-over; it starts closed so results are what you see. */
  readonly filtersOpen = signal(false);

  readonly suggestions = signal<LocationSuggestion[]>([]);
  readonly landmarkLabel = signal('');

  /** The single source of truth, derived straight from the URL. */
  readonly filters = toSignal(this.route.queryParams.pipe(map(readFilters)), {
    initialValue: readFilters({}),
  });

  readonly pageSize = computed(() => this.filters().pageSize ?? PAGE_SIZE_DEFAULT);
  readonly page = computed(() => this.filters().page ?? 1);
  readonly lastPage = computed(() => Math.max(1, Math.ceil(this.total() / this.pageSize())));
  readonly hasFilters = computed(() => countActive(this.filters()) > 0);
  readonly activeCount = computed(() => countActive(this.filters()));
  readonly mapQuery = computed(() => writeFilters(this.filters()));

  /** The skeleton count matches the page size so the grid does not jump when results land. */
  readonly skeletons = computed(() => Array.from({ length: this.pageSize() }));

  constructor() {
    this.engagement.primeShortlist();

    this.reference.cities().subscribe((list) => this.cities.set(list ?? []));
    this.reference.facilities().subscribe((list) => this.facilities.set(list ?? []));

    // Every navigation - including the back button - re-reads the URL and re-fetches.
    this.route.queryParams.subscribe((params) => {
      const filters = readFilters(params);
      this.loadAreas(filters.cityId);
      this.fetch(filters);
    });
  }

  isSaved(id: number): boolean {
    return this.engagement.isSaved(id);
  }

  /** The return URL keeps the filters: signing in must not throw away the search. */
  toggleSave(id: number): void {
    this.engagement.toggle(id, this.router.url);
  }

  /**
   * Areas are only asked for once the city is known to exist. A hand-edited `cityId` would
   * otherwise fire a request that can only 404 - the cities list is already cached, so
   * checking costs nothing, and the invalid city is still reported by the 422 the search
   * itself comes back with.
   */
  private loadAreas(cityId: number | undefined): void {
    if (!cityId) {
      this.areas.set([]);
      return;
    }

    this.reference.cities().subscribe((cities) => {
      if (!cities.some((city) => city.id === cityId)) {
        this.areas.set([]);
        return;
      }
      this.reference.areas(cityId).subscribe((list) => this.areas.set(list ?? []));
    });
  }

  private fetch(filters: PropertyFilters): void {
    const problem = filterProblem(filters);
    if (problem) {
      this.loading.set(false);
      this.error.set(problem);
      this.results.set([]);
      this.total.set(0);
      return;
    }

    this.loading.set(true);
    this.error.set(null);

    this.properties.search(filters).subscribe({
      next: (page) => {
        this.results.set(page.items ?? []);
        this.total.set(page.totalCount ?? 0);
        this.loading.set(false);
      },
      error: (err) => {
        // An id that names nothing is a 422 with a sentence written to be read - "City
        // 999999 does not exist." Showing it verbatim beats an empty grid that looks like
        // a search which simply found nothing.
        this.error.set(toFailure(err).message);
        this.results.set([]);
        this.total.set(0);
        this.loading.set(false);
      },
    });
  }

  /** Any filter change resets to page 1: staying on page 4 of a different search is a bug. */
  apply(change: Partial<PropertyFilters>): void {
    this.navigate({ ...this.filters(), ...change, page: 1 });
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.lastPage()) return;
    this.navigate({ ...this.filters(), page });
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  clearAll(): void {
    this.landmarkLabel.set('');
    this.navigate({});
  }

  private navigate(filters: PropertyFilters): void {
    this.router.navigate(['/search'], { queryParams: writeFilters(filters) });
  }

  onCityChange(value: string): void {
    const cityId = value ? Number(value) : undefined;
    // The area belonged to the old city; keeping it would filter to an area the new city
    // does not contain and quietly return nothing.
    this.apply({ cityId, areaId: undefined });
  }

  toggleFacility(id: number): void {
    const current = this.filters().facilityIds ?? [];
    const next = current.includes(id) ? current.filter((f) => f !== id) : [...current, id];
    this.apply({ facilityIds: next });
  }

  isFacilityOn(id: number): boolean {
    return (this.filters().facilityIds ?? []).includes(id);
  }

  onLandmarkQuery(query: string): void {
    this.landmarkLabel.set(query);
    this.reference.suggest(query).subscribe((list) => this.suggestions.set(list));
  }

  chooseSuggestion(suggestion: LocationSuggestion): void {
    this.suggestions.set([]);
    this.landmarkLabel.set(suggestion.name);

    if (suggestion.type === 'Landmark') {
      // Picking a place is a request to see what is near it, so the nearest room comes
      // first unless somebody has already chosen a different order. Without this the
      // distances are all computed and shown, and the list still reads as ignoring them.
      this.apply({ landmarkId: suggestion.id, sort: this.filters().sort ?? 'Distance' });
      return;
    }
    if (suggestion.type === 'City') {
      this.apply({ cityId: suggestion.id, areaId: undefined, landmarkId: undefined });
      return;
    }
    this.apply({ areaId: suggestion.id, landmarkId: undefined });
  }

  clearLandmark(): void {
    this.landmarkLabel.set('');
    this.suggestions.set([]);
    // Distance means nothing without a landmark, and both are a 400 without one.
    this.apply({ landmarkId: undefined, maxDistanceKm: undefined, sort: undefined });
  }

  asNumber(value: string): number | undefined {
    const parsed = Number(value);
    return value === '' || Number.isNaN(parsed) ? undefined : parsed;
  }
}

/** Query string in. Anything unparseable is treated as absent rather than as a zero. */
export function readFilters(params: Params): PropertyFilters {
  const num = (key: string): number | undefined => {
    const raw = params[key];
    if (raw === undefined || raw === null || raw === '') return undefined;
    const parsed = Number(raw);
    return Number.isFinite(parsed) ? parsed : undefined;
  };

  const ids = String(params['facilityIds'] ?? '')
    .split(',')
    .map((part) => Number(part))
    .filter((part) => Number.isFinite(part) && part > 0);

  return {
    cityId: num('cityId'),
    areaId: num('areaId'),
    landmarkId: num('landmarkId'),
    maxDistanceKm: num('maxDistanceKm'),
    minRent: num('minRent'),
    maxRent: num('maxRent'),
    roomType: ROOM_TYPES.includes(params['roomType']) ? (params['roomType'] as RoomType) : undefined,
    genderPolicy: GENDER_POLICIES.includes(params['genderPolicy'])
      ? (params['genderPolicy'] as GenderPolicy)
      : undefined,
    facilityIds: ids.length ? ids : undefined,
    availableOnly: params['availableOnly'] === 'true' ? true : undefined,
    sort: SORT_OPTIONS.some((option) => option.value === params['sort'])
      ? (params['sort'] as PropertySort)
      : undefined,
    page: num('page') ?? 1,
    pageSize: num('pageSize') ?? PAGE_SIZE_DEFAULT,
  };
}

/**
 * Query string out. Defaults are left off so a plain search is `/search` rather than
 * `/search?page=1&pageSize=12`, and a shared link carries only what somebody chose.
 */
export function writeFilters(filters: PropertyFilters): Params {
  const params: Params = {};
  const put = (key: string, value: unknown) => {
    if (value !== undefined && value !== null && value !== '') params[key] = value;
  };

  put('cityId', filters.cityId);
  put('areaId', filters.areaId);
  put('landmarkId', filters.landmarkId);
  put('maxDistanceKm', filters.maxDistanceKm);
  put('minRent', filters.minRent);
  put('maxRent', filters.maxRent);
  put('roomType', filters.roomType);
  put('genderPolicy', filters.genderPolicy);
  put('facilityIds', filters.facilityIds?.length ? filters.facilityIds.join(',') : undefined);
  put('availableOnly', filters.availableOnly ? 'true' : undefined);
  put('sort', filters.sort);
  if (filters.page && filters.page > 1) params['page'] = filters.page;
  if (filters.pageSize && filters.pageSize !== PAGE_SIZE_DEFAULT) {
    params['pageSize'] = filters.pageSize;
  }

  return params;
}

/** How many filters a person actually set - paging and page size are not filters. */
function countActive(filters: PropertyFilters): number {
  const { page, pageSize, ...rest } = filters;
  return Object.values(rest).filter(
    (value) => value !== undefined && (!Array.isArray(value) || value.length > 0),
  ).length;
}
