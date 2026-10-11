import { CommonModule } from '@angular/common';
import { Component, ElementRef, OnDestroy, ViewChild, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Params, Router, RouterLink } from '@angular/router';
import * as L from 'leaflet';
import { LocationSuggestion, PropertyCard, PropertyFilters } from '../../core/models/catalog.model';
import { toFailure } from '../../core/http/api-error';
import { GeolocationService } from '../../core/services/geolocation.service';
import { PropertyService } from '../../core/services/property.service';
import { ReferenceService } from '../../core/services/reference.service';
import { MapBox, boxFilters, clampBox, formatBox, parseBox } from './map-box';
import { readFilters, writeFilters } from './search.component';

const MAP_PAGE_SIZE = 50;
const PAKISTAN_CENTRE: L.LatLngExpression = [30.3753, 69.3451];
const PLACE_ZOOM = 13;

/**
 * Public map search: the map fills the screen, a place search sits on top of it, and a
 * marker opens a small card with the way into the listing.
 *
 * Moving the map does not search by itself - a page that refetches on every drag is a page
 * that jumps under the person's hand. Once they have moved it, "Search this area" asks the
 * server for exactly the part of the map on screen (rules 133-134), and the box rides in the
 * URL as `box=south,west,north,east` so the view survives a refresh or a shared link.
 */
@Component({
  selector: 'app-map-search',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './map-search.component.html',
  styleUrls: ['./map-search.component.scss'],
})
export class MapSearchComponent implements OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly properties = inject(PropertyService);
  private readonly reference = inject(ReferenceService);
  private readonly geo = inject(GeolocationService);

  readonly filters = signal<PropertyFilters>(readFilters(this.route.snapshot.queryParams));
  readonly box = signal<MapBox | null>(parseBox(this.route.snapshot.queryParams['box']));
  readonly results = signal<PropertyCard[]>([]);
  readonly total = signal(0);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  /** The person has panned or zoomed since the last search, so "Search this area" means something. */
  readonly moved = signal(false);
  readonly fullscreen = signal(false);

  readonly query = signal('');
  readonly suggestions = signal<LocationSuggestion[]>([]);

  readonly listQuery = computed(() => writeFilters(this.filters()));

  /** Anything narrowing the map to a place or an area, which "Show all rooms" takes off. */
  readonly narrowed = computed(() => {
    const f = this.filters();
    return this.box() !== null || f.cityId != null || f.areaId != null || f.landmarkId != null;
  });

  readonly summary = computed(() => {
    if (this.loading()) return 'Loading rooms…';
    const total = this.total();
    if (total === 0) {
      return this.box() ? 'No rooms in this part of the map.' : 'No rooms match this search.';
    }
    const shown = this.results().length;
    const noun = total === 1 ? 'room' : 'rooms';
    return shown < total
      ? `Showing ${shown} of ${total} ${noun}. Zoom in to see the rest.`
      : `${total} ${noun} on the map.`;
  });

  @ViewChild('mapPage') private page?: ElementRef<HTMLElement>;

  private canvas?: ElementRef<HTMLElement>;
  private map?: L.Map;
  private readonly markers = new Map<number, L.Marker>();

  /** Moves made by this code end before this moment; only later ones count as the person's. */
  private quietUntil = 0;
  /** Set by "Search this area": the map is already showing the box, so leave the view alone. */
  private keepView = false;
  /** The place chosen in the search box, used to centre the map when it has no rooms. */
  private chosenPlace: LocationSuggestion | null = null;

  private readonly onFullscreenChange = () => {
    this.fullscreen.set(document.fullscreenElement === this.page?.nativeElement);
    this.fitToViewport();
  };

  /**
   * The header wraps to two lines on a phone and the verify banner can sit under it, so the
   * space left for the map is measured rather than assumed. The main area's bottom padding
   * is what it keeps clear for the phone's bottom navigation.
   */
  private readonly fitToViewport = () => {
    const page = this.page?.nativeElement;
    if (!page) return;

    if (this.fullscreen()) {
      page.style.height = '';
    } else {
      const top = page.getBoundingClientRect().top + window.scrollY;
      const main = page.closest('.app-main-content');
      const reserve = main ? parseFloat(getComputedStyle(main).paddingBottom) || 0 : 0;
      page.style.height = `${Math.max(360, Math.floor(window.innerHeight - top - reserve))}px`;
    }
    this.quietly(() => this.map?.invalidateSize());
  };

  @ViewChild('mapCanvas')
  set mapCanvas(value: ElementRef<HTMLElement> | undefined) {
    this.canvas = value;
    if (value) {
      queueMicrotask(() => this.ensureMap());
    } else {
      this.destroyMap();
    }
  }

  constructor() {
    document.addEventListener('fullscreenchange', this.onFullscreenChange);
    window.addEventListener('resize', this.fitToViewport);

    this.route.queryParams.subscribe((params) => {
      const filters = readFilters(params);
      this.filters.set(filters);
      this.box.set(parseBox(params['box']));
      this.fetch(filters, this.box());
    });
  }

  ngOnDestroy(): void {
    document.removeEventListener('fullscreenchange', this.onFullscreenChange);
    window.removeEventListener('resize', this.fitToViewport);
    this.destroyMap();
  }

  // --- place search ------------------------------------------------------------------------

  onQuery(value: string): void {
    this.query.set(value);
    this.reference.suggest(value).subscribe((list) => this.suggestions.set(list));
  }

  /**
   * A chosen place replaces whatever narrowed the map before it - an earlier place or a
   * dragged-out box - and keeps every other filter. A landmark also turns on distances.
   */
  choose(suggestion: LocationSuggestion): void {
    this.suggestions.set([]);
    this.query.set(suggestion.name);
    this.chosenPlace = suggestion;

    const isLandmark = suggestion.type === 'Landmark';
    const current = this.filters();
    const next: PropertyFilters = {
      ...current,
      cityId: suggestion.type === 'City' ? suggestion.id : undefined,
      areaId: suggestion.type === 'Area' ? suggestion.id : undefined,
      landmarkId: isLandmark ? suggestion.id : undefined,
      maxDistanceKm: isLandmark ? current.maxDistanceKm : undefined,
      sort: isLandmark ? 'Distance' : current.sort === 'Distance' ? undefined : current.sort,
      page: 1,
    };

    this.go(writeFilters(next));
  }

  /** Enter with nothing chosen takes the first suggestion rather than doing nothing. */
  submitPlace(): void {
    const first = this.suggestions()[0];
    if (first) this.choose(first);
  }

  closeSuggestions(): void {
    this.suggestions.set([]);
  }

  // --- map actions -------------------------------------------------------------------------

  /**
   * The box replaces the city and area, which would otherwise quietly cut it down to their
   * own overlap, and the distance cap, which is a second shape on top of it. A landmark
   * stays so the cards keep their distances.
   */
  searchThisArea(): void {
    if (!this.map) return;
    const bounds = this.map.getBounds();
    const box = clampBox({
      south: bounds.getSouth(),
      west: bounds.getWest(),
      north: bounds.getNorth(),
      east: bounds.getEast(),
    });

    const next: PropertyFilters = {
      ...this.filters(),
      cityId: undefined,
      areaId: undefined,
      maxDistanceKm: undefined,
      page: 1,
    };

    this.keepView = true;
    this.chosenPlace = null;
    this.query.set('');
    this.go({ ...writeFilters(next), box: formatBox(box) });
  }

  showAll(): void {
    const current = this.filters();
    const next: PropertyFilters = {
      ...current,
      cityId: undefined,
      areaId: undefined,
      landmarkId: undefined,
      maxDistanceKm: undefined,
      sort: current.sort === 'Distance' ? undefined : current.sort,
      page: 1,
    };
    this.chosenPlace = null;
    this.query.set('');
    this.go(writeFilters(next));
  }

  toggleFullscreen(): void {
    const element = this.page?.nativeElement;
    if (!element) return;
    if (document.fullscreenElement) {
      void document.exitFullscreen();
    } else {
      void element.requestFullscreen?.();
    }
  }

  get fullscreenSupported(): boolean {
    return typeof document !== 'undefined' && !!document.fullscreenEnabled;
  }

  private go(queryParams: Params): void {
    this.moved.set(false);
    void this.router.navigate([], { relativeTo: this.route, queryParams });
  }

  // --- data --------------------------------------------------------------------------------

  private fetch(filters: PropertyFilters, box: MapBox | null): void {
    const hasLandmark = filters.landmarkId != null;
    this.loading.set(true);
    this.error.set(null);
    this.moved.set(false);

    this.properties
      .search({
        ...filters,
        ...boxFilters(box),
        page: 1,
        pageSize: MAP_PAGE_SIZE,
        // Distance sorting and a distance cap are both a 400 without a landmark, so the map
        // falls back to the ordinary search when nobody has picked a place yet.
        maxDistanceKm: hasLandmark ? filters.maxDistanceKm : undefined,
        sort: hasLandmark ? filters.sort ?? 'Distance' : filters.sort === 'Distance' ? undefined : filters.sort,
      })
      .subscribe({
        next: (page) => {
          this.results.set(page.items ?? []);
          this.total.set(page.totalCount ?? 0);
          this.loading.set(false);
          queueMicrotask(() => {
            this.ensureMap();
            this.renderMarkers();
            this.frame();
          });
        },
        error: (error) => {
          this.error.set(toFailure(error).message);
          this.results.set([]);
          this.total.set(0);
          this.loading.set(false);
          this.clearMarkers();
        },
      });
  }

  // --- leaflet -----------------------------------------------------------------------------

  private ensureMap(): void {
    if (this.map || !this.canvas) return;

    this.map = L.map(this.canvas.nativeElement, {
      center: PAKISTAN_CENTRE,
      zoom: 5,
      zoomControl: false,
    });
    // Top-left, under the search box: bottom-right is where the assistant's launcher sits.
    L.control.zoom({ position: 'topleft' }).addTo(this.map);

    L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
      maxZoom: 19,
      attribution: '© OpenStreetMap contributors',
    }).addTo(this.map);

    this.map.on('moveend', () => {
      if (Date.now() >= this.quietUntil) this.moved.set(true);
    });
    // Opening a card near the edge pans the map to fit it. That is the map's move, not the
    // person's, and should not offer "Search this area".
    this.map.on('autopanstart', () => {
      this.quietUntil = Date.now() + 1000;
    });

    setTimeout(() => {
      this.fitToViewport();
      this.renderMarkers();
      this.frame();
    }, 0);
  }

  private destroyMap(): void {
    this.map?.remove();
    this.map = undefined;
    this.markers.clear();
  }

  /** Runs a move this code makes without it counting as the person moving the map. */
  private quietly(move: () => void): void {
    this.quietUntil = Date.now() + 400;
    move();
  }

  /**
   * Where the map should look after a search: at the box if there is one, around the rooms
   * if there are any, at the chosen place if it came back empty.
   */
  private frame(): void {
    const map = this.map;
    if (!map) return;

    if (this.keepView) {
      this.keepView = false;
      return;
    }

    const box = this.box();
    if (box) {
      this.quietly(() =>
        map.fitBounds(
          [
            [box.south, box.west],
            [box.north, box.east],
          ],
          { animate: false },
        ),
      );
      return;
    }

    const pins = this.results()
      .filter((p) => Number.isFinite(p.latitude) && Number.isFinite(p.longitude))
      .map((p) => [p.latitude, p.longitude] as L.LatLngTuple);

    if (pins.length) {
      this.quietly(() =>
        map.fitBounds(L.latLngBounds(pins), { padding: [64, 64], maxZoom: 15, animate: false }),
      );
      return;
    }

    const place = this.chosenPlace;
    if (place?.latitude != null && place.longitude != null) {
      this.quietly(() => map.setView([place.latitude!, place.longitude!], PLACE_ZOOM, { animate: false }));
    }
  }

  private renderMarkers(): void {
    if (!this.map) return;
    this.clearMarkers();

    for (const property of this.results()) {
      if (!Number.isFinite(property.latitude) || !Number.isFinite(property.longitude)) continue;

      const marker = L.marker([property.latitude, property.longitude], {
        keyboard: true,
        title: property.title,
        alt: `${property.title} map marker`,
        icon: markerIcon(false),
      })
        .bindPopup(() => this.popupFor(property), {
          minWidth: 240,
          maxWidth: 240,
          className: 'map-popup',
          // Clear of the search box and buttons on top and the status line at the bottom.
          autoPanPaddingTopLeft: [16, 140],
          autoPanPaddingBottomRight: [16, 72],
        })
        .addTo(this.map);

      marker.on('popupopen', () => marker.setIcon(markerIcon(true)));
      marker.on('popupclose', () => marker.setIcon(markerIcon(false)));
      this.markers.set(property.id, marker);
    }
  }

  private clearMarkers(): void {
    for (const marker of this.markers.values()) marker.remove();
    this.markers.clear();
  }

  /**
   * The marker's card. Built from elements and text rather than an HTML string, because
   * the title is the owner's own words and must never be read as markup.
   */
  private popupFor(property: PropertyCard): HTMLElement {
    const el = <K extends keyof HTMLElementTagNameMap>(tag: K, className: string, text?: string) => {
      const node = document.createElement(tag);
      node.className = className;
      if (text !== undefined) node.textContent = text;
      return node;
    };

    const card = el('div', 'map-card');
    card.dataset['smoke'] = 'map-popup';

    if (property.primaryThumbnailUrl) {
      const photo = el('img', 'map-card-photo');
      photo.src = property.primaryThumbnailUrl;
      photo.alt = '';
      photo.loading = 'lazy';
      card.append(photo);
    }

    const body = el('div', 'map-card-body');
    const heading = el('div', 'map-card-heading');
    heading.append(el('h3', 'map-card-title', property.title));
    if (property.hasInspectionBadge) heading.append(el('span', 'map-card-badge', 'Inspected'));
    body.append(heading);

    body.append(el('p', 'map-card-meta', `${property.areaName}, ${property.cityName}`));

    const rent = el('p', 'map-card-rent');
    rent.append(el('strong', '', `PKR ${property.monthlyRent.toLocaleString('en-PK')}`), ' / month');
    body.append(rent);

    const beds = property.availableBeds;
    body.append(el('p', 'map-card-meta', `${beds} ${beds === 1 ? 'bed' : 'beds'} available`));

    const distance = this.distanceLine(property);
    if (distance) body.append(el('p', 'map-card-meta', distance));

    const open = el('button', 'map-card-action', 'View listing');
    open.type = 'button';
    open.dataset['smoke'] = 'map-view-listing';
    open.addEventListener('click', () => void this.router.navigate(['/property', property.id]));
    body.append(open);

    card.append(body);
    return card;
  }

  /** The chosen place's distance when there is one, otherwise the person's own. */
  private distanceLine(property: PropertyCard): string | null {
    if (property.distanceKm != null) {
      return `${property.distanceKm.toFixed(1)} km from the place you chose (straight-line)`;
    }
    const km = this.geo.distanceTo(property.latitude, property.longitude);
    return km !== null ? `${km.toFixed(1)} km from you (straight-line)` : null;
  }
}

function markerIcon(selected: boolean): L.DivIcon {
  return L.divIcon({
    className: selected ? 'map-result-marker is-selected' : 'map-result-marker',
    html: '<span aria-hidden="true"></span>',
    iconSize: [24, 24],
    iconAnchor: [12, 12],
    popupAnchor: [0, -12],
  });
}
