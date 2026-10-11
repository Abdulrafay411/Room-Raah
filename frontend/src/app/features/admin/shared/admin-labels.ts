import { VerificationCheckType } from '../../../core/models/catalog.model';

/**
 * The words an admin reads, in one place. The API speaks in enum names - `PendingReview`,
 * `NotChecked`, `UnderReview` - and for a while the panel printed them as they arrived.
 * Every status the five tabs can show is mapped here with the tone its pill takes, so a
 * raw enum on screen is a missing row in this table rather than a habit.
 */
export type StatusTone = 'good' | 'bad' | 'warn' | 'info' | 'neutral';

const STATUS: Record<string, [label: string, tone: StatusTone]> = {
  // Listing lifecycle
  Draft: ['Draft', 'neutral'],
  PendingReview: ['Pending review', 'warn'],
  ChangesRequested: ['Changes requested', 'warn'],
  Published: ['Published', 'good'],
  Rejected: ['Rejected', 'bad'],
  Unpublished: ['Unpublished', 'bad'],
  // Verification checks and inspections
  NotChecked: ['Not checked', 'neutral'],
  Checked: ['Checked', 'good'],
  Failed: ['Failed', 'bad'],
  Passed: ['Passed', 'good'],
  // Reports
  Open: ['Open', 'warn'],
  UnderReview: ['Under review', 'info'],
  Upheld: ['Upheld', 'bad'],
  Dismissed: ['Dismissed', 'neutral'],
  // Reviews and revisions
  Pending: ['Pending', 'warn'],
  Approved: ['Approved', 'good'],
  Hidden: ['Hidden', 'bad'],
  Content: ['Content', 'info'],
  Availability: ['Availability', 'neutral'],
  // Accounts
  Active: ['Active', 'good'],
  Inactive: ['Inactive', 'bad'],
  Seeker: ['Seeker', 'neutral'],
  Owner: ['Owner', 'info'],
  Admin: ['Admin', 'info'],
};

export function statusLabel(value: string | null | undefined): string {
  if (!value) return '';
  return STATUS[value]?.[0] ?? sentenceCase(value);
}

export function statusTone(value: string | null | undefined): StatusTone {
  if (!value) return 'neutral';
  return STATUS[value]?.[1] ?? 'neutral';
}

/** The four checks that gate publishing, in the order an admin works through them. */
export const REQUIRED_CHECKS: VerificationCheckType[] = ['OwnerIdentity', 'Address', 'Photos', 'Facilities'];

export const CHECK_LABELS: Record<VerificationCheckType, { name: string; hint: string }> = {
  OwnerIdentity: { name: 'Owner identity', hint: 'The identity document on the account belongs to this person.' },
  Address: { name: 'Address', hint: 'The typed address and the pin on the map agree.' },
  Photos: { name: 'Photographs', hint: 'Real photographs of this room, nothing copied.' },
  Facilities: { name: 'Facilities', hint: 'What is ticked is actually there.' },
  Availability: { name: 'Availability', hint: 'Optional. The bed count looks right.' },
};

export function isRequiredCheck(type: VerificationCheckType): boolean {
  return REQUIRED_CHECKS.includes(type);
}

/** Field names in a content revision diff, as the owner's form labels them. */
const FIELDS: Record<string, string> = {
  title: 'Title',
  description: 'Description',
  roomType: 'Room type',
  genderPolicy: 'Who it is for',
  totalBeds: 'Beds in the room',
  availableBeds: 'Beds free',
  monthlyRent: 'Monthly rent',
  securityDeposit: 'Security deposit',
  utilitiesCharge: 'Utilities a month',
  messCharge: 'Mess a month',
  houseRules: 'House rules',
  addressLine: 'Address',
  areaId: 'Area',
  areaName: 'Area',
  cityId: 'City',
  cityName: 'City',
  latitude: 'Map pin (latitude)',
  longitude: 'Map pin (longitude)',
  facilities: 'Facilities',
  facilityIds: 'Facilities',
};

export function fieldLabel(field: string): string {
  return FIELDS[field] ?? sentenceCase(field);
}

/** `PendingReview` → `Pending review`, `owner_identity` → `Owner identity`. */
export function sentenceCase(value: string): string {
  const spaced = value
    .replace(/[_-]+/g, ' ')
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .trim()
    .toLowerCase();
  return spaced.charAt(0).toUpperCase() + spaced.slice(1);
}
