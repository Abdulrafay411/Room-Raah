/**
 * The four things a seeker can do to a listing rather than just look at it: shortlist it,
 * ask to visit it, review it afterwards, and report it. Taken from the brief's sections
 * 8.4 to 8.7; where the prose and `RoomRaah.openapi.json` disagree the OpenAPI file wins.
 */
import { PropertyCard, StayStatus } from './catalog.model';

export type VisitType = 'Physical' | 'Video';
export type VisitStatus = 'Requested' | 'Accepted' | 'Declined' | 'Completed' | 'Cancelled';
export type ModerationState = 'Pending' | 'Approved' | 'Hidden';
export type ReportReason =
  | 'Inaccurate'
  | 'Misleading'
  | 'AlreadyTaken'
  | 'Scam'
  | 'Offensive'
  | 'Other';
export type ReportStatus = 'Open' | 'UnderReview' | 'Upheld' | 'Dismissed';

export const VISIT_TYPES: ReadonlyArray<{ value: VisitType; label: string }> = [
  { value: 'Physical', label: 'In person' },
  { value: 'Video', label: 'Video call' },
];

export const STAY_STATUSES: ReadonlyArray<{ value: StayStatus; label: string }> = [
  { value: 'Visited', label: 'I visited but did not stay' },
  { value: 'CurrentlyStaying', label: 'I am staying here now' },
  { value: 'PreviouslyStayed', label: 'I stayed here before' },
];

/**
 * Built from the enumeration, not from the seed data, per the brief. The labels are the
 * interface's; the values go on the wire exactly as spelled here.
 */
export const REPORT_REASONS: ReadonlyArray<{ value: ReportReason; label: string }> = [
  { value: 'Inaccurate', label: 'Something here is wrong' },
  { value: 'Misleading', label: 'The photos or description mislead' },
  { value: 'AlreadyTaken', label: 'The room is already taken' },
  { value: 'Scam', label: 'This looks like a scam' },
  { value: 'Offensive', label: 'Offensive content' },
  { value: 'Other', label: 'Something else' },
];

/**
 * A visit request as both sides of it see it. Rule 93: the seeker is a display name and
 * the owner is not named at all - there is nothing in this shape to reach either of them
 * with off the site, which is the whole reason visit requests exist.
 */
export interface VisitRequest {
  id: number;
  propertyId: number;
  propertyTitle: string;
  areaName: string;
  cityName: string;
  seekerDisplayName: string;
  visitType: VisitType;
  preferredAt: string;
  status: VisitStatus;
  /** What the owner wrote back. Required on a decline: rule 89. */
  ownerResponseNote: string | null;
  respondedAt: string | null;
  createdAt: string;
}

export interface CreateVisitRequest {
  propertyId: number;
  visitType: VisitType;
  preferredAt: string;
}

/** A card plus the moment it went on the shortlist. The list is ordered by that. */
export interface SavedListing extends PropertyCard {
  savedAt: string;
}

export interface CreateReview {
  propertyId: number;
  rating: number;
  comment: string;
  stayStatus: StayStatus;
}

/**
 * The author's own view of the review they just wrote. It carries the moderation state
 * because the author is the one person entitled to know their review is waiting: without
 * it, rule 41 looks exactly like the review having vanished.
 */
export interface MyReview {
  id: number;
  propertyId: number;
  rating: number;
  comment: string;
  stayStatus: StayStatus;
  moderationState: ModerationState;
  createdAt: string;
}

export interface CreateReport {
  propertyId: number;
  reason: ReportReason;
  details: string;
}

export interface MyReport {
  id: number;
  propertyId: number;
  reason: ReportReason;
  details: string;
  status: ReportStatus;
  createdAt: string;
}

/** The brief's window: `preferredAt` must be in the future and at most 60 days ahead. */
export const VISIT_MAX_DAYS_AHEAD = 60;

/** Reviews are capped at 1000 characters; a report's details at the same. */
export const REVIEW_COMMENT_MAX = 1000;
export const REPORT_DETAILS_MAX = 1000;

/**
 * The state machine the buttons must follow, expressed as the three questions the
 * interface actually asks of a request. Getting these wrong shows a person a button that
 * can only fail, which is worse than not offering it.
 */

/** Open means the listing cannot take another request from this person: a second is a 422. */
export function isOpenVisit(visit: VisitRequest): boolean {
  return visit.status === 'Requested' || visit.status === 'Accepted';
}

/** The seeker's, and only from Requested or Accepted. */
export function canCancel(visit: VisitRequest): boolean {
  return isOpenVisit(visit);
}

/**
 * Only from Accepted, and only once `preferredAt` has passed - the server refuses earlier.
 * This is the button that unlocks writing a review, so an interface that never surfaces it
 * is an interface where nobody can review anything.
 */
export function canComplete(visit: VisitRequest, now: Date = new Date()): boolean {
  return visit.status === 'Accepted' && new Date(visit.preferredAt).getTime() <= now.getTime();
}

/** A review is possible once a visit to that listing reached Completed, and once only. */
export function canReview(visit: VisitRequest): boolean {
  return visit.status === 'Completed';
}

/**
 * The two things the server answers 400 to on a visit request. Catching them here means the
 * person is told before they submit rather than after, which is what the brief asks for -
 * and it keeps a round trip that can only fail off the wire.
 *
 * Returns the reason, or null when the request is sendable.
 */
export function visitProblem(preferredAt: string, now: Date = new Date()): string | null {
  if (!preferredAt) return 'Choose when you would like to visit.';

  const when = new Date(preferredAt);
  if (Number.isNaN(when.getTime())) return 'That is not a date we can read.';
  if (when.getTime() <= now.getTime()) return 'Choose a time in the future.';

  const limit = new Date(now.getTime());
  limit.setDate(limit.getDate() + VISIT_MAX_DAYS_AHEAD);
  if (when.getTime() > limit.getTime()) {
    return `Visits can be arranged up to ${VISIT_MAX_DAYS_AHEAD} days ahead.`;
  }
  return null;
}

/**
 * `<input type="datetime-local">` reads and writes `YYYY-MM-DDTHH:mm` in local time and
 * refuses anything else, including the trailing `Z` an ISO string carries. These two turn
 * a Date into that, and back into what the API is sent.
 */
export function toLocalInputValue(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return (
    `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}` +
    `T${pad(date.getHours())}:${pad(date.getMinutes())}`
  );
}

/** The earliest and latest a picker may offer, so it cannot produce the wrong side. */
export function visitWindow(now: Date = new Date()): { min: string; max: string } {
  const min = new Date(now.getTime() + 60 * 60 * 1000);
  const max = new Date(now.getTime());
  max.setDate(max.getDate() + VISIT_MAX_DAYS_AHEAD);
  return { min: toLocalInputValue(min), max: toLocalInputValue(max) };
}
