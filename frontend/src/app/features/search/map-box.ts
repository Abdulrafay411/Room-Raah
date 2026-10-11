import { PropertyFilters } from '../../core/models/catalog.model';

/** The part of the map somebody asked to search, edges in degrees. */
export interface MapBox {
  south: number;
  west: number;
  north: number;
  east: number;
}

/**
 * Four decimals is about eleven metres. Rounding keeps the URL short and lets two people
 * looking at nearly the same view share one cached page on the server.
 */
const PLACES = 4;

const round = (value: number): number => Number(value.toFixed(PLACES));

/**
 * `box=south,west,north,east` out of the map's query string, or null when it is missing or
 * would be refused by the server: a box with swapped edges or a point off the globe is
 * treated as no box rather than as a request that is bound to fail.
 */
export function parseBox(raw: unknown): MapBox | null {
  if (typeof raw !== 'string' || raw.trim() === '') return null;

  const parts = raw.split(',').map((part) => Number(part));
  if (parts.length !== 4 || parts.some((part) => !Number.isFinite(part))) return null;

  const [south, west, north, east] = parts;
  const onGlobe =
    [south, north].every((lat) => lat >= -90 && lat <= 90) &&
    [west, east].every((lng) => lng >= -180 && lng <= 180);

  if (!onGlobe || south > north || west > east) return null;
  return { south, west, north, east };
}

export function formatBox(box: MapBox): string {
  return [box.south, box.west, box.north, box.east].map(round).join(',');
}

/**
 * Leaflet reports a view that has been panned around the world with longitudes past 180.
 * The server refuses those, so the box is clamped to the globe before it is sent.
 */
export function clampBox(box: MapBox): MapBox {
  const clamp = (value: number, limit: number) => Math.min(limit, Math.max(-limit, value));
  return {
    south: round(clamp(box.south, 90)),
    west: round(clamp(box.west, 180)),
    north: round(clamp(box.north, 90)),
    east: round(clamp(box.east, 180)),
  };
}

/** The search parameters a box adds, in the names the server reads. */
export function boxFilters(box: MapBox | null): Partial<PropertyFilters> {
  if (!box) return {};
  return {
    minLatitude: box.south,
    maxLatitude: box.north,
    minLongitude: box.west,
    maxLongitude: box.east,
  };
}
