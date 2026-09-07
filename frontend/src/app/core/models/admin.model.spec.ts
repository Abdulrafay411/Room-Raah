import { describe, expect, it } from 'vitest';
import { inspectionProblem, requiredNote } from './admin.model';

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
