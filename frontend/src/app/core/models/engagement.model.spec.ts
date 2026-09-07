import { describe, expect, it } from 'vitest';
import {
  VisitRequest,
  canCancel,
  canComplete,
  canReview,
  isOpenVisit,
  toLocalInputValue,
  visitProblem,
  visitWindow,
} from './engagement.model';

/**
 * The visit state machine and the date window. Both are logic that can break without
 * anything on screen looking wrong: a button that is offered one state too early produces a
 * 422, and a date one hour on the wrong side of the window produces a 400. Neither is
 * something a person scanning the page would catch, which is exactly why they are tested
 * here rather than left to the smoke walk.
 */

const NOW = new Date('2026-09-07T12:00:00Z');

function visit(overrides: Partial<VisitRequest>): VisitRequest {
  return {
    id: 1,
    propertyId: 1,
    propertyTitle: 'A room',
    areaName: 'Gulberg',
    cityName: 'Lahore',
    seekerDisplayName: 'Hamza Iqbal',
    visitType: 'Physical',
    preferredAt: '2026-09-08T12:00:00Z',
    status: 'Requested',
    ownerResponseNote: null,
    respondedAt: null,
    createdAt: '2026-09-01T12:00:00Z',
    ...overrides,
  };
}

describe('the visit state machine', () => {
  it('treats only Requested and Accepted as open', () => {
    expect(isOpenVisit(visit({ status: 'Requested' }))).toBe(true);
    expect(isOpenVisit(visit({ status: 'Accepted' }))).toBe(true);
    expect(isOpenVisit(visit({ status: 'Declined' }))).toBe(false);
    expect(isOpenVisit(visit({ status: 'Completed' }))).toBe(false);
    expect(isOpenVisit(visit({ status: 'Cancelled' }))).toBe(false);
  });

  it('offers cancel from Requested and Accepted, and nowhere else', () => {
    expect(canCancel(visit({ status: 'Requested' }))).toBe(true);
    expect(canCancel(visit({ status: 'Accepted' }))).toBe(true);
    expect(canCancel(visit({ status: 'Completed' }))).toBe(false);
    expect(canCancel(visit({ status: 'Cancelled' }))).toBe(false);
  });

  it('withholds complete until the visit time has passed', () => {
    const accepted = visit({ status: 'Accepted', preferredAt: '2026-09-08T12:00:00Z' });
    expect(canComplete(accepted, NOW)).toBe(false);

    const past = visit({ status: 'Accepted', preferredAt: '2026-09-06T12:00:00Z' });
    expect(canComplete(past, NOW)).toBe(true);
  });

  it('does not offer complete on a request the owner has not accepted', () => {
    const past = visit({ status: 'Requested', preferredAt: '2026-09-06T12:00:00Z' });
    expect(canComplete(past, NOW)).toBe(false);
  });

  it('unlocks a review only once the visit is Completed', () => {
    expect(canReview(visit({ status: 'Completed' }))).toBe(true);
    expect(canReview(visit({ status: 'Accepted' }))).toBe(false);
  });
});

describe('the visit date window', () => {
  it('refuses a time in the past', () => {
    expect(visitProblem('2026-09-06T12:00', NOW)).toBe('Choose a time in the future.');
  });

  it('refuses more than sixty days ahead', () => {
    expect(visitProblem('2026-12-01T12:00', NOW)).toContain('60 days');
  });

  it('accepts something inside the window', () => {
    expect(visitProblem('2026-09-20T12:00', NOW)).toBeNull();
  });

  it('asks for a date rather than sending an empty one', () => {
    expect(visitProblem('', NOW)).toBe('Choose when you would like to visit.');
  });

  it('offers a picker window that cannot produce either refusal', () => {
    const { min, max } = visitWindow(NOW);
    expect(visitProblem(min, NOW)).toBeNull();
    expect(visitProblem(max, NOW)).toBeNull();
  });
});

describe('the local datetime format', () => {
  // `<input type="datetime-local">` refuses anything but `YYYY-MM-DDTHH:mm` - an ISO string
  // with its seconds and trailing Z reads to the browser as an empty field.
  it('has no seconds and no zone', () => {
    expect(toLocalInputValue(new Date(2026, 8, 7, 9, 5))).toBe('2026-09-07T09:05');
  });
});
