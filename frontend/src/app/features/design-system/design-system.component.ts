import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  ButtonComponent,
  BadgeComponent,
  FormFieldComponent,
  InputDirective,
  EmptyStateComponent,
  DialogComponent,
  PropertyCardComponent,
  PropertyCardItem,
  SpinnerComponent,
  SkeletonComponent,
} from '../../shared/components';

@Component({
  selector: 'app-design-system',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ButtonComponent,
    BadgeComponent,
    FormFieldComponent,
    InputDirective,
    EmptyStateComponent,
    DialogComponent,
    PropertyCardComponent,
    SpinnerComponent,
    SkeletonComponent,
  ],
  templateUrl: './design-system.component.html',
  styleUrls: ['./design-system.component.scss'],
})
export class DesignSystemComponent {
  // Dialog state
  isDialogOpen = signal(false);
  isDialogLoading = signal(false);

  // Button demo states
  isButtonLoading = signal(false);
  isShortlistSaved = signal(false);

  // Form field demo states
  textInputValue = signal('');
  hasFormError = signal(false);
  formErrorMessage = signal('This field is required');

  // Sample listing matching GET /properties DTO exactly (Brief §8.3)
  sampleProperty: PropertyCardItem = {
    id: 4,
    title: 'Single room with sea breeze, Clifton',
    areaId: 4,
    areaName: 'Clifton',
    cityId: 2,
    cityName: 'Karachi',
    roomType: 'Private',
    genderPolicy: 'Girls',
    totalBeds: 1,
    availableBeds: 1,
    monthlyRent: 32000,
    securityDeposit: 32000,
    utilitiesCharge: 4000,
    messCharge: 0,
    latitude: 24.8138,
    longitude: 67.03,
    primaryThumbnailUrl: 'https://images.unsplash.com/photo-1522771739844-6a9f6d5f14af?auto=format&fit=crop&w=600&q=80',
    photoCount: 4,
    hasInspectionBadge: true,
    availabilityConfirmedAt: '2026-09-06T10:50:00.51751',
    distanceKm: 1.2,
  };

  sampleFullProperty: PropertyCardItem = {
    id: 5,
    title: 'Budget shared space near FAST NUCES, Faisal Town',
    areaId: 1,
    areaName: 'Faisal Town',
    cityId: 1,
    cityName: 'Lahore',
    roomType: 'Shared',
    genderPolicy: 'Boys',
    totalBeds: 4,
    availableBeds: 0,
    monthlyRent: 14000,
    securityDeposit: 14000,
    utilitiesCharge: 2500,
    messCharge: 6000,
    latitude: 31.478,
    longitude: 74.305,
    primaryThumbnailUrl: 'https://images.unsplash.com/photo-1555854877-bab0e564b8d5?auto=format&fit=crop&w=600&q=80',
    photoCount: 6,
    hasInspectionBadge: false,
    availabilityConfirmedAt: '2026-09-05T08:00:00',
    distanceKm: null,
  };

  openDialog(): void {
    this.isDialogOpen.set(true);
  }

  closeDialog(): void {
    this.isDialogOpen.set(false);
  }

  toggleDialogLoading(): void {
    this.isDialogLoading.set(true);
    setTimeout(() => {
      this.isDialogLoading.set(false);
      this.isDialogOpen.set(false);
    }, 1500);
  }

  toggleButtonLoading(): void {
    this.isButtonLoading.set(true);
    setTimeout(() => {
      this.isButtonLoading.set(false);
    }, 2000);
  }

  togglePropertySaved(propertyId: number): void {
    this.isShortlistSaved.update((val) => !val);
  }

  toggleErrorState(): void {
    this.hasFormError.update((val) => !val);
  }
}
