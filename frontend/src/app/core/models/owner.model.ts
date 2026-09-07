/**
 * An owner's own view of their listings. Taken from the brief's section 8.9.
 *
 * The thing that shapes every screen built on these types: an owner never edits a published
 * listing directly. A `PUT` on something Published creates a revision and leaves the live
 * listing exactly as it was, and the response says so by carrying `pendingRevision`. The
 * copy has to follow - "Sent for review", never "Saved".
 */
import { Facility, GenderPolicy, RoomType } from './catalog.model';

export type PropertyStatus =
  | 'Draft'
  | 'PendingReview'
  | 'ChangesRequested'
  | 'Published'
  | 'Rejected'
  | 'Unpublished';

export type PhotoStatus = 'Live' | 'PendingAdd' | 'PendingRemove';
export type RevisionKind = 'Content' | 'Availability';
export type RevisionStatus = 'Pending' | 'Approved' | 'Rejected';

/** A listing needs between these many live photographs before it can be submitted. */
export const PHOTOS_MIN = 3;
export const PHOTOS_MAX = 15;

/** The server decodes the bytes, so renaming a .txt to .jpg does not get past it. */
export const PHOTO_MAX_BYTES = 5 * 1024 * 1024;
export const PHOTO_TYPES = ['image/jpeg', 'image/png', 'image/webp'];

export interface ListingPhoto {
  id: number;
  url: string;
  thumbnailUrl: string;
  isPrimary: boolean;
  sortOrder: number;
  /** On a published listing a new photo is `PendingAdd` and a deleted one `PendingRemove`. */
  status: PhotoStatus;
  uploadedAt: string;
}

/**
 * What the list endpoint answers with, which is deliberately not the detail shape - no
 * photographs, no facilities, no coordinates. Modelling the two as one type was a real bug:
 * the dashboard counted `photos` on a summary that never carries any and threw on every
 * row. `photoCount` is what the list gives instead.
 */
export interface MyListingSummary {
  id: number;
  title: string;
  status: PropertyStatus;
  areaName: string;
  cityName: string;
  monthlyRent: number;
  availableBeds: number;
  totalBeds: number;
  photoCount: number;
  primaryThumbnailUrl: string | null;
  hasInspectionBadge: boolean;
  availabilityConfirmedAt: string | null;
  updatedAt: string | null;
}

export interface MyListing {
  id: number;
  title: string;
  description: string;
  addressLine: string;
  latitude: number;
  longitude: number;
  roomType: RoomType;
  genderPolicy: GenderPolicy;
  totalBeds: number;
  availableBeds: number;
  monthlyRent: number;
  securityDeposit: number;
  utilitiesCharge: number;
  messCharge: number;
  houseRules: string | null;
  status: PropertyStatus;
  hasInspectionBadge: boolean;
  availabilityConfirmedAt: string | null;
  areaId: number;
  areaName: string;
  cityId: number;
  cityName: string;
  facilities: Facility[];
  photos: ListingPhoto[];
  createdAt: string;
  updatedAt: string | null;
  /**
   * Present only when the write just made created a revision instead of changing the
   * listing. Its presence is the signal to say "sent for review" rather than "saved".
   */
  pendingRevision?: PendingRevision | null;
}

export interface PendingRevision {
  id: number;
  kind: RevisionKind;
  status: RevisionStatus;
  createdAt: string;
}

/** What create and update both send. Every field is required except `houseRules`. */
export interface ListingDraft {
  areaId: number;
  title: string;
  description: string;
  addressLine: string;
  latitude: number;
  longitude: number;
  roomType: RoomType;
  genderPolicy: GenderPolicy;
  totalBeds: number;
  availableBeds: number;
  monthlyRent: number;
  securityDeposit: number;
  utilitiesCharge: number;
  messCharge: number;
  houseRules: string;
  facilityIds: number[];
}

/** Somewhere central in Lahore, so a new listing's map opens over Pakistan and not the sea. */
export const DEFAULT_PIN = { latitude: 31.5204, longitude: 74.3587 };

export function emptyDraft(): ListingDraft {
  return {
    areaId: 0,
    title: '',
    description: '',
    addressLine: '',
    latitude: DEFAULT_PIN.latitude,
    longitude: DEFAULT_PIN.longitude,
    roomType: 'Private',
    genderPolicy: 'Boys',
    totalBeds: 1,
    availableBeds: 1,
    monthlyRent: 0,
    securityDeposit: 0,
    utilitiesCharge: 0,
    messCharge: 0,
    houseRules: '',
    facilityIds: [],
  };
}

export function toDraft(listing: MyListing): ListingDraft {
  return {
    areaId: listing.areaId,
    title: listing.title,
    description: listing.description,
    addressLine: listing.addressLine,
    latitude: listing.latitude,
    longitude: listing.longitude,
    roomType: listing.roomType,
    genderPolicy: listing.genderPolicy,
    totalBeds: listing.totalBeds,
    availableBeds: listing.availableBeds,
    monthlyRent: listing.monthlyRent,
    securityDeposit: listing.securityDeposit,
    utilitiesCharge: listing.utilitiesCharge,
    messCharge: listing.messCharge,
    houseRules: listing.houseRules ?? '',
    facilityIds: listing.facilities.map((facility) => facility.id),
  };
}

/**
 * What a `PUT` will actually do, which is the single most important thing to tell an owner
 * before they press the button. Getting this wrong means somebody thinks a live listing has
 * changed when it has not, or thinks nothing happened when a revision is waiting.
 */
export function saveEffect(status: PropertyStatus): 'edit' | 'revision' | 'locked' {
  if (status === 'Published') return 'revision';
  if (status === 'PendingReview') return 'locked';
  return 'edit';
}

export function saveLabel(status: PropertyStatus): string {
  switch (saveEffect(status)) {
    case 'revision':
      return 'Send changes for review';
    case 'locked':
      return 'Being reviewed';
    default:
      return 'Save';
  }
}

/**
 * What the owner is told about what just happened. `pendingRevision` on the response is the
 * authority here, not the status we sent - the listing could have been published between
 * the form loading and the save.
 */
export function saveOutcome(listing: MyListing): string {
  return listing.pendingRevision
    ? 'Sent for review. Your live listing has not changed yet.'
    : 'Saved.';
}

/** Submit works only from these two. Anywhere else the button would only produce a 422. */
export function canSubmit(status: PropertyStatus): boolean {
  return status === 'Draft' || status === 'ChangesRequested';
}

/** Only live photographs count towards the three a submission needs. */
export function livePhotoCount(listing: MyListing): number {
  return listing.photos.filter((photo) => photo.status === 'Live').length;
}

/**
 * Why the submit button will be refused, said before it is pressed rather than after.
 * Returns null when the listing is ready to go.
 *
 * Takes a count rather than a listing because the two screens that ask have different
 * shapes in hand: the form knows which photographs are live, the dashboard only knows how
 * many there are. That is not a gap - pending photographs exist only on a published
 * listing, and submit is only ever offered on a draft, so on the dashboard the two numbers
 * are the same number.
 */
export function submitProblem(status: PropertyStatus, livePhotos: number): string | null {
  if (!canSubmit(status)) {
    return status === 'PendingReview'
      ? 'This listing is with the review team already.'
      : 'Only a draft or a listing you have been asked to change can be sent for review.';
  }

  if (livePhotos < PHOTOS_MIN) {
    const missing = PHOTOS_MIN - livePhotos;
    return `Add ${missing} more photograph${missing === 1 ? '' : 's'} — a listing needs at least ${PHOTOS_MIN}.`;
  }
  return null;
}

/** What is wrong with a file before it is uploaded, or null when nothing is. */
export function photoProblem(file: File): string | null {
  if (!PHOTO_TYPES.includes(file.type)) {
    return 'Photographs must be JPEG, PNG or WebP.';
  }
  if (file.size > PHOTO_MAX_BYTES) {
    return 'That photograph is over 5 MB. Try a smaller one.';
  }
  return null;
}

export function statusLabel(status: PropertyStatus): string {
  switch (status) {
    case 'PendingReview':
      return 'With the review team';
    case 'ChangesRequested':
      return 'Changes requested';
    default:
      return status;
  }
}

export function statusVariant(
  status: PropertyStatus,
): 'confirmed' | 'warning' | 'danger' | 'neutral' {
  if (status === 'Published') return 'confirmed';
  if (status === 'PendingReview' || status === 'ChangesRequested') return 'warning';
  if (status === 'Rejected' || status === 'Unpublished') return 'danger';
  return 'neutral';
}
