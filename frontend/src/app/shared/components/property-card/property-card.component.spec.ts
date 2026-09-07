import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { PropertyCardComponent } from './property-card.component';
import { PropertyCardItem } from './property-card.model';
import { GeolocationService } from '../../../core/services/geolocation.service';

describe('PropertyCardComponent', () => {
  let component: PropertyCardComponent;
  let fixture: ComponentFixture<PropertyCardComponent>;

  const mockProperty: PropertyCardItem = {
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
    primaryThumbnailUrl: 'http://127.0.0.1:10000/devstoreaccount1/properties/4/thumb.jpg',
    photoCount: 4,
    hasInspectionBadge: true,
    availabilityConfirmedAt: '2026-09-06T10:50:00.51751',
    distanceKm: 1.2,
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PropertyCardComponent],
      providers: [provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(PropertyCardComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('property', mockProperty);
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should render title and location', () => {
    const titleEl = fixture.nativeElement.querySelector('.card-title');
    expect(titleEl.textContent).toContain('Single room with sea breeze, Clifton');

    const locationEl = fixture.nativeElement.querySelector('.location-text');
    expect(locationEl.textContent).toContain('Clifton, Karachi');
  });

  // The card measures from the person, not from a landmark, so the distance only exists
  // once a position does. Both halves matter: silence without one, and a labelled
  // straight-line figure with one.
  it('shows no distance until the browser has given a position', () => {
    expect(fixture.nativeElement.querySelector('.distance-text')).toBeNull();
  });

  it('displays straight-line distance from the person once a position is known', () => {
    // Clifton, about 2.2km along the coast from the mock listing.
    TestBed.inject(GeolocationService).position.set({ lat: 24.8138, lng: 67.0522 });
    fixture.detectChanges();

    const distanceEl = fixture.nativeElement.querySelector('.distance-text');
    expect(distanceEl).toBeTruthy();
    expect(distanceEl.textContent).toContain('km from you (straight-line)');
    expect(distanceEl.textContent).toContain('2.2 km');
  });

  it('should render inspection badge when hasInspectionBadge is true', () => {
    const badgeEl = fixture.nativeElement.querySelector('.badge-inspection');
    expect(badgeEl).toBeTruthy();
    expect(badgeEl.textContent).toContain('Inspected');
  });

  it('should render Full tag when availableBeds is 0', () => {
    fixture.componentRef.setInput('property', {
      ...mockProperty,
      availableBeds: 0,
    });
    fixture.detectChanges();

    const dangerBadge = fixture.nativeElement.querySelector('.badge-danger');
    expect(dangerBadge).toBeTruthy();
    expect(dangerBadge.textContent).toContain('Full (0 beds)');
  });

  it('should render skeleton when loading is true', () => {
    fixture.componentRef.setInput('loading', true);
    fixture.detectChanges();

    const skeletonEl = fixture.nativeElement.querySelector('.property-card.is-skeleton');
    expect(skeletonEl).toBeTruthy();
  });

  it('should toggle save and emit event when shortlist button is clicked', () => {
    let toggledId: number | null = null;
    component.saveToggled.subscribe((id) => {
      toggledId = id;
    });

    const saveBtn: HTMLButtonElement = fixture.nativeElement.querySelector('.save-button');
    expect(saveBtn.getAttribute('aria-label')).toContain('Save Single room with sea breeze, Clifton to shortlist');

    saveBtn.click();
    expect(toggledId).toBe(4);
  });
});
