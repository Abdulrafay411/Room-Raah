import { describe, expect, it } from 'vitest';
import { inspectionProblem, isListingSearch, listingNumber, requiredNote } from './admin.model';

describe('admin decisions', () => {
  it('requires an auditable reason where the screen says one is required', () => {
    expect(requiredNote('   ')).toBe('Write a reason before making this decision.');
    expect(requiredNote('Photos do not match.')).toBeNull();
  });
});

describe('inspection dates', () => {
  it('does not send an empty listing id', () => {
    expect(inspectionProblem(0, '2020-01-01T10:00')).toBe('Choose a listing.');
  });

  it('refuses a future inspection', () => {
    expect(inspectionProblem(1, '2999-01-01T10:00')).toBe(
      'An inspection cannot be recorded in the future.',
    );
  });

  it('accepts a completed inspection', () => {
    expect(inspectionProblem(1, '2020-01-01T10:00')).toBeNull();
  });
});

describe('finding a listing for an inspection (rule 135)', () => {
  it('reads a number, with or without a hash, as a listing number', () => {
    expect(listingNumber('12')).toBe(12);
    expect(listingNumber(' #29 ')).toBe(29);
  });

  it('does not read a name, zero or a mixed string as a number', () => {
    expect(listingNumber('Gulberg')).toBeNull();
    expect(listingNumber('0')).toBeNull();
    expect(listingNumber('12 Gulberg')).toBeNull();
  });

  it('searches only for at least two characters that are not a number', () => {
    expect(isListingSearch('Lahore')).toBe(true);
    expect(isListingSearch('  la ')).toBe(true);
    expect(isListingSearch('L')).toBe(false);
    expect(isListingSearch('29')).toBe(false);
    expect(isListingSearch('x'.repeat(151))).toBe(false);
  });
});
