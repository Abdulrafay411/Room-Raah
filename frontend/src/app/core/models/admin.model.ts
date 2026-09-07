import { Facility, Page, PropertyPhoto, VerificationCheckType, VerificationStatus } from './catalog.model';
import { PropertyStatus } from './owner.model';

export type RevisionKind = 'Content' | 'Availability';
export type ReviewState = 'Pending' | 'Approved' | 'Hidden';
export type ReportStatus = 'Open' | 'UnderReview' | 'Upheld' | 'Dismissed';
export type AccountRole = 'Seeker' | 'Owner' | 'Admin';

export interface AdminPropertySummary {
  id: number;
  title: string;
  status: PropertyStatus;
  areaName: string;
  cityName: string;
  ownerName: string;
  monthlyRent: number;
  photoCount: number;
  primaryThumbnailUrl: string | null;
  updatedAt: string;
}

export interface AdminListingOwner {
  id: number;
  fullName: string;
  identityStatus: VerificationStatus;
  identityCheckedAt: string | null;
}

export interface AdminVerificationCheck {
  checkType: VerificationCheckType;
  status: VerificationStatus;
  evidenceDate: string | null;
  note: string | null;
  recordedAt: string;
}

export interface AdminProperty {
  id: number;
  title: string;
  description: string;
  addressLine: string;
  latitude: number;
  longitude: number;
  roomType: string;
  genderPolicy: string;
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
  photos: PropertyPhoto[];
  createdAt: string;
  updatedAt: string;
  owner: AdminListingOwner;
  verificationChecks: AdminVerificationCheck[];
  canPublish: boolean;
}

export interface VerificationDraft {
  checkType: VerificationCheckType;
  status: VerificationStatus;
  evidenceDate: string | null;
  note: string | null;
}

export interface RevisionQueueRow {
  id: number;
  propertyId: number;
  propertyTitle: string;
  kind: RevisionKind;
  areaName: string;
  cityName: string;
  ownerName: string;
  changedFields: string[];
  currentAvailableBeds: number;
  proposedAvailableBeds: number;
  totalBeds: number;
  submittedAt: string;
}

export interface FieldChange {
  field: string;
  current: string | null;
  proposed: string | null;
}

export interface RevisionDetail {
  id: number;
  propertyId: number;
  propertyTitle: string;
  kind: RevisionKind;
  reviewStatus: 'Pending' | 'Approved' | 'Rejected';
  ownerName: string;
  changes: FieldChange[];
  currentPhotos: PropertyPhoto[];
  photosToAdd: PropertyPhoto[];
  photosToRemove: PropertyPhoto[];
  proposedPhotoOrder: number[] | null;
  submittedAt: string;
  reviewedAt: string | null;
  adminNote: string | null;
}

export interface DueInspection {
  propertyId: number;
  title: string;
  areaName: string;
  cityName: string;
  ownerDisplayName: string;
  lastInspectionAt: string | null;
  daysOverdue: number;
}

export interface InspectionRecord {
  id: number;
  propertyId: number;
  inspectedAt: string;
  result: 'Passed' | 'Failed';
  notes: string | null;
  feeAmount: number | null;
  feeCollectedAt: string | null;
  inspectorDisplayName: string;
}

export interface ReviewQueueRow {
  id: number;
  propertyId: number;
  authorDisplayName: string;
  rating: number;
  comment: string;
  stayStatus: string;
  createdAt: string;
  moderationState: ReviewState;
  propertyTitle: string;
  moderatedAt: string | null;
}

export interface ReportRow {
  id: number;
  propertyId: number;
  reason: string;
  details: string;
  status: ReportStatus;
  createdAt: string;
  propertyTitle: string;
  reportedByDisplayName: string;
  adminNote: string | null;
  resolvedByAdminId: number | null;
  resolvedAt: string | null;
}

/** Deliberately excludes the two identity fields carried only by the detail endpoint. */
export interface AdminUserSummary {
  id: number;
  fullName: string;
  email: string;
  role: AccountRole;
  isEmailVerified: boolean;
  isActive: boolean;
  createdAt: string;
}

/** Rule 116's one Admin-only detail shape. Never cache or log an instance of this type. */
export interface AdminUserDetail extends AdminUserSummary {
  phoneNumber: string | null;
  cnicNumber: string | null;
  identityStatus: VerificationStatus | null;
  identityCheckedAt: string | null;
  canInspect: boolean | null;
}

export type AdminPropertyPage = Page<AdminPropertySummary>;
export type RevisionPage = Page<RevisionQueueRow>;
export type InspectionPage = Page<DueInspection>;
export type ReviewQueuePage = Page<ReviewQueueRow>;
export type ReportPage = Page<ReportRow>;
export type AdminUserPage = Page<AdminUserSummary>;

export const CHECK_TYPES: VerificationCheckType[] = [
  'OwnerIdentity',
  'Address',
  'Photos',
  'Facilities',
  'Availability',
];

export function requiredNote(value: string): string | null {
  return value.trim() ? null : 'Write a reason before making this decision.';
}

export function inspectionProblem(propertyId: number, inspectedAt: string): string | null {
  if (!Number.isInteger(propertyId) || propertyId <= 0) return 'Choose a listing.';
  if (!inspectedAt) return 'Choose when the inspection happened.';
  const when = new Date(inspectedAt);
  if (Number.isNaN(when.getTime())) return 'That is not a date we can read.';
  if (when.getTime() > Date.now()) return 'An inspection cannot be recorded in the future.';
  return null;
}
