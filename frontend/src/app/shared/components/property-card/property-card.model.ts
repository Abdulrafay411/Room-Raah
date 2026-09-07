// Property item shape strictly as defined by API GET /properties (Brief §8.3)
export interface PropertyCardItem {
  id: number;
  title: string;
  areaId: number;
  areaName: string;
  cityId: number;
  cityName: string;
  roomType: string;         // 'Private' | 'Shared'
  genderPolicy: string;     // 'Boys' | 'Girls' | 'Any'
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
  distanceKm: number | null;
}
