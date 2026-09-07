import {
  AfterViewInit,
  Component,
  ElementRef,
  EventEmitter,
  Input,
  OnChanges,
  OnDestroy,
  Output,
  SimpleChanges,
  ViewChild,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import * as L from 'leaflet';

/**
 * A draggable pin on an OpenStreetMap map, which is where a listing's coordinates come from.
 *
 * There is no geocoder anywhere in this product: an address is typed, a pin is dropped, and
 * an admin checks that the two agree. So this component's only job is to turn a drag into a
 * latitude and a longitude, and to keep showing the pin somebody already placed.
 *
 * Leaflet's default marker is an image loaded by a relative URL that a bundler moves, which
 * is the classic way this ends up as a missing-image icon in production. A `divIcon` avoids
 * the problem outright: the pin is a styled element, so there is no asset to lose.
 */
@Component({
  selector: 'app-map-pin',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './map-pin.component.html',
  styleUrls: ['./map-pin.component.scss'],
})
export class MapPinComponent implements AfterViewInit, OnChanges, OnDestroy {
  @Input() latitude = 0;
  @Input() longitude = 0;
  @Input() zoom = 14;
  /** A read-only map still shows where a listing is; only the form lets the pin move. */
  @Input() draggable = true;
  @Input() label = 'Drag the pin to where the room is';

  @Output() moved = new EventEmitter<{ latitude: number; longitude: number }>();

  @ViewChild('canvas') canvas?: ElementRef<HTMLElement>;

  private map?: L.Map;
  private marker?: L.Marker;

  ngAfterViewInit(): void {
    if (!this.canvas) return;

    this.map = L.map(this.canvas.nativeElement, {
      center: [this.latitude, this.longitude],
      zoom: this.zoom,
      // The map is one control on a long form; letting it swallow the page scroll is the
      // fastest way to make a form impossible to get past on a laptop.
      scrollWheelZoom: false,
      attributionControl: true,
    });

    L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
      maxZoom: 19,
      attribution: '© OpenStreetMap contributors',
    }).addTo(this.map);

    this.marker = L.marker([this.latitude, this.longitude], {
      draggable: this.draggable,
      icon: L.divIcon({ className: 'map-pin-marker', iconSize: [22, 22], iconAnchor: [11, 22] }),
      keyboard: true,
      title: this.label,
    }).addTo(this.map);

    if (this.draggable) {
      this.marker.on('dragend', () => this.emit());
      // Tapping is how most people will do this on a phone, where dragging a pin inside a
      // scrolling page is genuinely awkward.
      this.map.on('click', (event: L.LeafletMouseEvent) => {
        this.marker?.setLatLng(event.latlng);
        this.emit();
      });
    }

    // The map measures its container on creation, and inside a form that is often still
    // being laid out at that moment. One resize on the next frame settles it.
    setTimeout(() => this.map?.invalidateSize(), 0);
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (!this.marker || !this.map) return;
    if (changes['latitude'] || changes['longitude']) {
      const next = L.latLng(this.latitude, this.longitude);
      if (!next.equals(this.marker.getLatLng())) {
        this.marker.setLatLng(next);
        this.map.panTo(next);
      }
    }
  }

  ngOnDestroy(): void {
    this.map?.remove();
    this.map = undefined;
    this.marker = undefined;
  }

  private emit(): void {
    const at = this.marker?.getLatLng();
    if (!at) return;
    // Six decimals is about a tenth of a metre. Anything beyond it is noise the server
    // stores and nobody can act on.
    this.moved.emit({
      latitude: Number(at.lat.toFixed(6)),
      longitude: Number(at.lng.toFixed(6)),
    });
  }
}
