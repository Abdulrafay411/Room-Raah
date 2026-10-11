import { describe, expect, it } from 'vitest';
import { boxFilters, clampBox, formatBox, parseBox } from './map-box';

describe('map box', () => {
  it('reads south, west, north, east from the query string', () => {
    expect(parseBox('31.4,74.2,31.6,74.4')).toEqual({
      south: 31.4,
      west: 74.2,
      north: 31.6,
      east: 74.4,
    });
  });

  it('treats a missing, short or non-numeric box as no box', () => {
    expect(parseBox(undefined)).toBeNull();
    expect(parseBox('')).toBeNull();
    expect(parseBox('31.4,74.2,31.6')).toBeNull();
    expect(parseBox('a,b,c,d')).toBeNull();
  });

  it('treats a box the server would refuse as no box', () => {
    expect(parseBox('31.6,74.2,31.4,74.4')).toBeNull();
    expect(parseBox('31.4,74.4,31.6,74.2')).toBeNull();
    expect(parseBox('-91,74.2,31.6,74.4')).toBeNull();
    expect(parseBox('31.4,74.2,31.6,181')).toBeNull();
  });

  it('writes four decimals so the URL stays short', () => {
    expect(formatBox({ south: 31.412345, west: 74.2, north: 31.6, east: 74.456789 })).toBe(
      '31.4123,74.2,31.6,74.4568',
    );
  });

  it('round-trips through the query string', () => {
    const box = { south: 24.8, west: 67, north: 25, east: 67.2 };
    expect(parseBox(formatBox(box))).toEqual(box);
  });

  it('clamps a view panned past the edge of the world', () => {
    expect(clampBox({ south: -95, west: -200, north: 95, east: 400 })).toEqual({
      south: -90,
      west: -180,
      north: 90,
      east: 180,
    });
  });

  it('names the edges the way the server reads them', () => {
    expect(boxFilters({ south: 1, west: 2, north: 3, east: 4 })).toEqual({
      minLatitude: 1,
      maxLatitude: 3,
      minLongitude: 2,
      maxLongitude: 4,
    });
    expect(boxFilters(null)).toEqual({});
  });
});
