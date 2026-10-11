import { describe, expect, it } from 'vitest';
import {
  emptyDraft,
  isDirty,
  nextStep,
  readiness,
  revisionFieldLabel,
  revisionSince,
  submitProblem,
  unwrapWrite,
  MyListing,
} from './owner.model';

const listing = {
  id: 4,
  title: 'A room',
  status: 'Published',
  facilities: [],
  photos: [],
} as unknown as MyListing;

describe('a write on a listing', () => {
  it('reads the shape the OpenAPI description gives: listing and revision side by side', () => {
    const saved = unwrapWrite({
      listing,
      pendingRevision: { id: 9, kind: 'Content', reviewStatus: 'Pending', submittedAt: '2026-10-01T09:00:00Z' },
    });
    expect(saved.id).toBe(4);
    expect(saved.pendingRevision?.id).toBe(9);
  });

  it('still reads a flat listing', () => {
    const saved = unwrapWrite({ ...listing, pendingRevision: null });
    expect(saved.id).toBe(4);
    expect(saved.pendingRevision).toBeNull();
  });

  it('accepts either spelling of when a revision was sent', () => {
    expect(revisionSince({ id: 1, kind: 'Content', submittedAt: 'a' })).toBe('a');
    expect(revisionSince({ id: 1, kind: 'Content', createdAt: 'b' })).toBe('b');
    expect(revisionSince({ id: 1, kind: 'Content' })).toBeNull();
  });
});

describe('what to do next', () => {
  it('counts the photographs a draft still needs', () => {
    expect(nextStep('Draft', 1)).toContain('2 more photographs');
    expect(nextStep('Draft', 2)).toContain('1 more photograph,');
    expect(nextStep('Draft', 3)).toContain('Send it for review');
  });

  it('says a live listing is reviewed before it changes', () => {
    expect(nextStep('Published', 5)).toMatch(/reviewed before they show/);
  });
});

describe('readiness', () => {
  it('lists what a blank listing is missing', () => {
    const items = readiness(emptyDraft(), 0);
    expect(items.every((item) => !item.done)).toBe(true);
    expect(items.find((item) => item.label.startsWith('At least'))?.label).toBe('At least 3 photographs (0 of 3)');
  });

  it('agrees with the submit rule on photographs', () => {
    const draft = { ...emptyDraft(), title: 't', description: 'd', areaId: 1, addressLine: 'a', monthlyRent: 1 };
    expect(readiness(draft, 3).every((item) => item.done)).toBe(true);
    expect(submitProblem('Draft', 3)).toBeNull();
    expect(readiness(draft, 2).filter((item) => !item.done)).toHaveLength(1);
    expect(submitProblem('Draft', 2)).not.toBeNull();
  });
});

describe('dirty tracking', () => {
  it('is clean against itself and after a no-op facility reorder', () => {
    const a = { ...emptyDraft(), facilityIds: [1, 2] };
    expect(isDirty(a, a)).toBe(false);
    expect(isDirty(a, { ...a, facilityIds: [2, 1] })).toBe(false);
  });

  it('notices a changed field or facility', () => {
    const a = emptyDraft();
    expect(isDirty({ ...a, title: 'x' }, a)).toBe(true);
    expect(isDirty({ ...a, facilityIds: [3] }, a)).toBe(true);
  });
});

describe('revision field labels', () => {
  it('uses the words on the form', () => {
    expect(revisionFieldLabel('monthlyRent')).toBe('Monthly rent');
    expect(revisionFieldLabel('somethingElse')).toBe('Something else');
  });
});
