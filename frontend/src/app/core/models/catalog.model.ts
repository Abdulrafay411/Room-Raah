/**
 * Shapes the public catalogue is built from - search, the listing, reviews and the
 * reference data the filters are drawn from. Taken from the brief's sections 6 and 8;
 * where prose and `RoomRaah.openapi.json` disagree the OpenAPI file wins.
 */

/** Every paged endpoint answers in this envelope. `page` starts at 1. */
export interface Page<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export const PAGE_SIZE_DEFAULT = 12;
/** The server refuses anything larger with a 400 rather than clamping it. */
export const PAGE_SIZE_MAX = 50;

export type RoomType = 'Private' | 'Shared';
export type GenderPolicy = 'Boys' | 'Girls' | 'Family';
export type PropertySort = 'Recent' | 'PriceLowToHigh' | 'PriceHighToLow' | 'Distance';
export type VerificationCheckType =
  | 'OwnerIdentity'
  | 'Address'
  | 'Photos'
  | 'Facilities'
  | 'Availability';
export type VerificationStatus = 'NotChecked' | 'Checked' | 'Failed';
export type StayStatus = 'Visited' | 'CurrentlyStaying' | 'PreviouslyStayed';

export const ROOM_TYPES: RoomType[] = ['Private', 'Shared'];
export const GENDER_POLICIES: GenderPolicy[] = ['Boys', 'Girls', 'Family'];

/**
 * Built from the enumeration rather than from whatever the seed data happens to contain,
 * per the brief. The labels are the interface's; the values are the API's and are sent
 * exactly as spelled here - a number, or different casing on the wire, is a 400.
 */
export const SORT_OPTIONS: ReadonlyArray<{ value: PropertySort; label: string }> = [
  { value: 'Recent', label: 'Most recent' },
  { value: 'PriceLowToHigh', label: 'Price: low to high' },
  { value: 'PriceHighToLow', label: 'Price: high to low' },
  { value: 'Distance', label: 'Distance' },
];

export interface Facility {
  id: number;
  name: string;
  iconKey: string;
}

export interface City {
  id: number;
  name: string;
}

export interface Area {
  id: number;
  name: string;
  cityId: number;
}

export type SuggestionType = 'City' | 'Area' | 'Landmark';
export type LandmarkKind = 'University' | 'Workplace' | 'Other';

/** One list mixing three kinds of place. Read `type` before doing anything with it. */
export interface LocationSuggestion {
  type: SuggestionType;
  id: number;
  name: string;
  city?: string;
  kind?: LandmarkKind;
  latitude?: number;
  longitude?: number;
}

/** What a card shows. The same fields are the head of the detail response. */
export interface PropertyCard {
  id: number;
  title: string;
  areaId: number;
  areaName: string;
  cityId: number;
  cityName: string;
  roomType: RoomType;
  genderPolicy: GenderPolicy;
  totalBeds: number;
  availableBeds: number;
  monthlyRent: number;
  securityDeposit: number;
  utilitiesCharge: number;
  messCharge: number;
  latitude: number;
  longitude: number;
  primaryThumbnailUrl: string | null;
  photoCount: number;
  hasInspectionBadge: boolean;
  availabilityConfirmedAt: string | null;
  /** Straight-line, one decimal, and null unless the search named a landmark. */
  distanceKm: number | null;
}

export interface PropertyPhoto {
  id: number;
  url: string;
  thumbnailUrl: string;
  isPrimary: boolean;
  sortOrder: number;
}

/**
 * Everything a seeker may know about the person behind a listing. No direct line to them
 * appears in this shape, and none is missing: reaching an owner happens through messaging
 * and visit requests, which is the entire reason both features exist.
 */
export interface ListingOwner {
  displayName: string;
  identityVerified: boolean;
  identityCheckedAt: string | null;
}

export interface VerificationCheck {
  checkType: VerificationCheckType;
  status: VerificationStatus;
  evidenceDate: string | null;
}

export interface PropertyDetail extends PropertyCard {
  description: string;
  addressLine: string;
  houseRules: string | null;
  facilities: Facility[];
  photos: PropertyPhoto[];
  owner: ListingOwner;
  checks: VerificationCheck[];
  createdAt: string;
}

export interface Review {
  id: number;
  propertyId: number;
  authorDisplayName: string;
  rating: number;
  comment: string;
  stayStatus: StayStatus;
  createdAt: string;
}

/** Reviews come back with the average alongside the page, so it has its own envelope. */
export interface ReviewPage extends Page<Review> {
  averageRating: number;
}

/**
 * Every search filter, all optional. Kept as one object because it round-trips through the
 * query string: filters live there so a filtered search is shareable and the back button
 * works, which is why page 08 of the brief has no route of its own.
 */
export interface PropertyFilters {
  cityId?: number;
  areaId?: number;
  landmarkId?: number;
  maxDistanceKm?: number;
  minRent?: number;
  maxRent?: number;
  roomType?: RoomType;
  genderPolicy?: GenderPolicy;
  facilityIds?: number[];
  availableOnly?: boolean;
  sort?: PropertySort;
  page?: number;
  pageSize?: number;
}

/**
 * Three combinations the server answers 400 to. Catching them here means the person is
 * told what is wrong instead of watching a request fail, and it keeps a pointless round
 * trip off the wire.
 *
 * Returns the reason, or null when the filters are sendable.
 */
export function filterProblem(filters: PropertyFilters): string | null {
  if (filters.maxDistanceKm != null && filters.landmarkId == null) {
    return 'Choose a university or workplace before filtering by distance.';
  }
  if (filters.sort === 'Distance' && filters.landmarkId == null) {
    return 'Choose a university or workplace before sorting by distance.';
  }
  if (filters.minRent != null && filters.maxRent != null && filters.minRent > filters.maxRent) {
    return 'The lowest rent cannot be more than the highest.';
  }
  return null;
}
