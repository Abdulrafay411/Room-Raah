import { CommonModule } from '@angular/common';
import { Component, ElementRef, OnDestroy, ViewChild, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import * as L from 'leaflet';
import { PropertyCard, PropertyFilters } from '../../core/models/catalog.model';
import { toFailure } from '../../core/http/api-error';
import { GeolocationService } from '../../core/services/geolocation.service';
import { PropertyService } from '../../core/services/property.service';
import { EmptyStateComponent } from '../../shared/components';
import { readFilters, writeFilters } from './search.component';

const MAP_PAGE_SIZE = 50;
const PAKISTAN_CENTRE: L.LatLngExpression = [30.3753, 69.3451];

/** Public map search. The list beside the map is the keyboard and screen-reader equivalent. */
@Component({
  selector: 'app-map-search',
  standalone: true,
  imports: [CommonModule, RouterLink, EmptyStateComponent],
  templateUrl: './map-search.component.html',
  styleUrls: ['./map-search.component.scss'],
})
export class MapSearchComponent implements OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly properties = inject(PropertyService);
  private readonly geo = inject(GeolocationService);

  readonly filters = signal<PropertyFilters>(readFilters(this.route.snapshot.queryParams));
  readonly results = signal<PropertyCard[]>([]);
  readonly total = signal(0);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly selectedId = signal<number | null>(null);
  readonly noLandmark = computed(() => this.filters().landmarkId == null);
  readonly listQuery = computed(() => writeFilters(this.filters()));

  private canvas?: ElementRef<HTMLElement>;
  private map?: L.Map;
  private readonly markers = new Map<number, L.Marker>();

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
    this.route.queryParams.subscribe((params) => {
      const filters = readFilters(params);
      this.filters.set(filters);
      this.selectedId.set(null);
      this.fetch(filters);
    });
  }

  ngOnDestroy(): void {
    this.destroyMap();
  }

  private destroyMap(): void {
    this.map?.remove();
    this.map = undefined;
    this.markers.clear();
  }

  /** Straight-line kilometres from the person to this room, or null if unknown. */
  distanceFromYou(property: PropertyCard): number | null {
    return this.geo.distanceTo(property.latitude, property.longitude);
  }

  select(property: PropertyCard): void {
    this.selectedId.set(property.id);
    this.updateMarkerIcons();
    const marker = this.markers.get(property.id);
    if (marker && this.map) {
      // "Show on map" should reveal the street-level area, not merely move the same
      // country-wide view. Animation stays off so reduced-motion users get the same result.
      this.map.setView(marker.getLatLng(), Math.max(this.map.getZoom(), 13), { animate: false });
    }
  }

  private fetch(filters: PropertyFilters): void {
    const hasLandmark = filters.landmarkId != null;
    this.loading.set(true);
    this.error.set(null);

    this.properties
      .search({
        ...filters,
        page: 1,
        pageSize: MAP_PAGE_SIZE,
        // Distance sorting and a distance cap are both a 400 without a landmark, so the map
        // falls back to the ordinary search when nobody has picked a place yet.
        maxDistanceKm: hasLandmark ? filters.maxDistanceKm : undefined,
        sort: hasLandmark ? filters.sort ?? 'Distance' : filters.sort,
      })
      .subscribe({
        next: (page) => {
          this.results.set(page.items ?? []);
          this.total.set(page.totalCount ?? 0);
          this.loading.set(false);
          queueMicrotask(() => {
            this.ensureMap();
            this.renderMarkers();
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

  private ensureMap(): void {
    if (this.map || !this.canvas) return;

    this.map = L.map(this.canvas.nativeElement, {
      center: PAKISTAN_CENTRE,
      zoom: 5,
      scrollWheelZoom: false,
    });

    L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
      maxZoom: 19,
      attribution: '© OpenStreetMap contributors',
    }).addTo(this.map);

    setTimeout(() => {
      this.map?.invalidateSize();
      this.renderMarkers();
    }, 0);
  }

  private renderMarkers(): void {
    if (!this.map) return;
    this.clearMarkers();

    const bounds: L.LatLngExpression[] = [];
    for (const property of this.results()) {
      if (!Number.isFinite(property.latitude) || !Number.isFinite(property.longitude)) continue;

      const position: L.LatLngExpression = [property.latitude, property.longitude];
      const marker = L.marker(position, {
        keyboard: true,
        title: `Show ${property.title} in the results list`,
        alt: `${property.title} map marker`,
        icon: markerIcon(false),
      }).addTo(this.map);

      marker.on('click', () => this.select(property));
      this.markers.set(property.id, marker);
      bounds.push(position);
    }

    if (bounds.length) {
      this.map.fitBounds(L.latLngBounds(bounds), { padding: [32, 32], maxZoom: 15 });
    }
  }

  private clearMarkers(): void {
    for (const marker of this.markers.values()) marker.remove();
    this.markers.clear();
  }

  private updateMarkerIcons(): void {
    for (const [id, marker] of this.markers) {
      marker.setIcon(markerIcon(id === this.selectedId()));
    }
  }
}

function markerIcon(selected: boolean): L.DivIcon {
  return L.divIcon({
    className: selected ? 'map-result-marker is-selected' : 'map-result-marker',
    html: '<span aria-hidden="true"></span>',
    iconSize: [24, 24],
    iconAnchor: [12, 12],
  });
}
