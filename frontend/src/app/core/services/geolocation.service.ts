import { Injectable, computed, signal } from '@angular/core';

export interface Coords {
  lat: number;
  lng: number;
}

export type GeolocationState = 'idle' | 'asking' | 'granted' | 'denied' | 'unavailable';

const STORAGE_KEY = 'roomraah.position';
const EARTH_RADIUS_KM = 6371;

/**
 * "How far is this room from *me*", answered in the browser.
 *
 * The server measures distance only from a seeded landmark - it takes a `landmarkId` and
 * nothing else - so a distance from wherever the person actually is cannot come from the
 * API. It does not need to: every card carries `latitude` and `longitude`, so once the
 * browser has told us where the person is, the arithmetic is a haversine over two points we
 * already hold.
 *
 * The position is asked for once and kept for the session. Every screen reads the same
 * signal, so granting the permission on search also fills in the distances on compare, the
 * map and the listing page without asking again.
 */
@Injectable({ providedIn: 'root' })
export class GeolocationService {
  readonly position = signal<Coords | null>(null);
  readonly state = signal<GeolocationState>('idle');

  /** True only when a real position is held, which is what every distance label hangs on. */
  readonly hasPosition = computed(() => this.position() !== null);

  constructor() {
    // Geolocation is a secure-context API: it exists on https and on localhost, and is
    // simply absent over plain http. The deployed demo is http, so this is the ordinary
    // case there rather than an error, and every distance falls back to a dash.
    if (typeof navigator === 'undefined' || !('geolocation' in navigator) || !isSecure()) {
      this.state.set('unavailable');
      return;
    }

    const saved = readSaved();
    if (saved) {
      this.position.set(saved);
      this.state.set('granted');
      return;
    }

    // Already granted in an earlier visit: fetch without a prompt so the distances are
    // simply there. Permissions is not in every browser, hence the optional chain.
    navigator.permissions
      ?.query({ name: 'geolocation' as PermissionName })
      .then((status) => {
        if (status.state === 'granted') this.request();
        if (status.state === 'denied') this.state.set('denied');
      })
      .catch(() => undefined);
  }

  request(): void {
    if (this.state() === 'unavailable' || this.state() === 'asking') return;
    this.state.set('asking');

    navigator.geolocation.getCurrentPosition(
      (result) => {
        const coords = { lat: result.coords.latitude, lng: result.coords.longitude };
        this.position.set(coords);
        this.state.set('granted');
        save(coords);
      },
      () => {
        // Refused, timed out or the device could not answer. They are the same thing to
        // every screen reading this - no distance - and none of them is worth an alert.
        this.position.set(null);
        this.state.set('denied');
        save(null);
      },
      { enableHighAccuracy: false, timeout: 10000, maximumAge: 5 * 60 * 1000 },
    );
  }

  forget(): void {
    this.position.set(null);
    this.state.set('idle');
    save(null);
  }

  /** Straight-line kilometres from the person to a listing, or null if we do not know. */
  distanceTo(latitude: number | null | undefined, longitude: number | null | undefined): number | null {
    const from = this.position();
    if (!from || !Number.isFinite(latitude ?? NaN) || !Number.isFinite(longitude ?? NaN)) {
      return null;
    }
    return haversineKm(from, { lat: latitude as number, lng: longitude as number });
  }
}

/** Rule 13's straight line, done locally. Good to a few metres at city scale. */
export function haversineKm(a: Coords, b: Coords): number {
  const toRad = (value: number) => (value * Math.PI) / 180;
  const dLat = toRad(b.lat - a.lat);
  const dLng = toRad(b.lng - a.lng);
  const lat1 = toRad(a.lat);
  const lat2 = toRad(b.lat);

  const h =
    Math.sin(dLat / 2) ** 2 + Math.sin(dLng / 2) ** 2 * Math.cos(lat1) * Math.cos(lat2);
  return 2 * EARTH_RADIUS_KM * Math.asin(Math.min(1, Math.sqrt(h)));
}

function isSecure(): boolean {
  return typeof window !== 'undefined' && window.isSecureContext === true;
}

function readSaved(): Coords | null {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as Coords;
    return Number.isFinite(parsed?.lat) && Number.isFinite(parsed?.lng) ? parsed : null;
  } catch {
    return null;
  }
}

function save(coords: Coords | null): void {
  try {
    if (coords) sessionStorage.setItem(STORAGE_KEY, JSON.stringify(coords));
    else sessionStorage.removeItem(STORAGE_KEY);
  } catch {
    // Private mode and blocked storage are not worth failing a page over.
  }
}
