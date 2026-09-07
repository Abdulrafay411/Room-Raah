import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { BadgeComponent, ButtonComponent } from '../../shared/components';
import { ReportDialogComponent } from './report-dialog.component';
import { AuthService } from '../../core/auth/auth.service';
import { CompareStore } from '../../core/services/compare.store';
import { EngagementService } from '../../core/services/engagement.service';
import { MessagingService } from '../../core/services/messaging.service';
import { GeolocationService } from '../../core/services/geolocation.service';
import { PropertyService } from '../../core/services/property.service';
import { toFailure } from '../../core/http/api-error';
import {
  PropertyDetail,
  Review,
  VerificationCheck,
  VerificationCheckType,
} from '../../core/models/catalog.model';

/** The five checks, in the order the product talks about them. */
const CHECK_LABELS: Record<VerificationCheckType, string> = {
  OwnerIdentity: 'Owner identity',
  Address: 'Address',
  Photos: 'Photographs',
  Facilities: 'Facilities',
  Availability: 'Availability',
};

/**
 * One listing.
 *
 * The thing to know before changing anything here: no direct line to the owner appears on
 * this page, and none is missing. `owner` is a display name and whether their identity was
 * checked - that is the entire shape the API returns to a seeker. Reaching them happens
 * through messaging and visit requests, which is why both features exist. A button
 * promising anything faster would have nothing to put in it.
 */
@Component({
  selector: 'app-property-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, BadgeComponent, ButtonComponent, ReportDialogComponent],
  templateUrl: './property-detail.component.html',
  styleUrls: ['./property-detail.component.scss'],
})
export class PropertyDetailComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly properties = inject(PropertyService);
  private readonly geo = inject(GeolocationService);
  private readonly engagement = inject(EngagementService);
  private readonly messaging = inject(MessagingService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  /** Public: the template both reads the selection and adds this room to it. */
  readonly compare = inject(CompareStore);

  /** Straight-line kilometres from the person to this room, or null if unknown. */
  distanceFromYou(latitude: number, longitude: number): number | null {
    return this.geo.distanceTo(latitude, longitude);
  }

  readonly listing = signal<PropertyDetail | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  readonly reviews = signal<Review[]>([]);
  readonly averageRating = signal(0);
  readonly reviewCount = signal(0);

  readonly activePhoto = signal(0);
  readonly reportOpen = signal(false);

  readonly threadBusy = signal(false);
  readonly threadError = signal<string | null>(null);

  /**
   * Only a seeker opens a thread - there is no directory of people to start one from, so
   * an owner or an admin reading this page has nothing to press. Signed out is not one of
   * those cases: that button works, and lands on the sign-in screen.
   */
  readonly canStartThread = computed(() => {
    const role = this.auth.userRole();
    return role === null || role === 'Seeker';
  });

  readonly photos = computed(() => this.listing()?.photos ?? []);
  readonly hasBeds = computed(() => (this.listing()?.availableBeds ?? 0) > 0);

  /** The one photograph every card shows. Rule 15: exactly one is primary. */
  readonly primaryPhoto = computed(
    () => this.photos().find((photo) => photo.isPrimary) ?? this.photos()[0] ?? null,
  );

  readonly monthlyTotal = computed(() => {
    const listing = this.listing();
    if (!listing) return 0;
    return listing.monthlyRent + listing.utilitiesCharge + listing.messCharge;
  });

  constructor() {
    // So the save button knows which way round it is before anybody presses it, and the
    // visit button knows whether this person already has a request open on this room.
    this.engagement.primeShortlist();
    this.engagement.primeOpenVisits();

    this.route.paramMap.subscribe((params) => {
      const id = Number(params.get('id'));
      if (!Number.isFinite(id) || id <= 0) {
        this.loading.set(false);
        this.error.set('That listing does not exist.');
        return;
      }
      this.load(id);
    });
  }

  private load(id: number): void {
    this.loading.set(true);
    this.error.set(null);
    this.activePhoto.set(0);

    this.properties.detail(id).subscribe({
      next: (listing) => {
        this.listing.set(listing);
        this.loading.set(false);
      },
      error: (err) => {
        // A 404 here means unpublished as well as absent, and the message says which.
        this.error.set(toFailure(err).message);
        this.loading.set(false);
      },
    });

    // The first few reviews sit on this page; the rest have their own. A failure to load
    // them must not take the listing down with it, so it is swallowed into an empty list.
    this.properties.reviews(id, 1, 3).subscribe({
      next: (page) => {
        this.reviews.set(page.items ?? []);
        this.averageRating.set(page.averageRating ?? 0);
        this.reviewCount.set(page.totalCount ?? 0);
      },
      error: () => this.reviews.set([]),
    });
  }

  checkLabel(check: VerificationCheck): string {
    return CHECK_LABELS[check.checkType] ?? check.checkType;
  }

  checkVariant(check: VerificationCheck): 'confirmed' | 'danger' | 'neutral' {
    if (check.status === 'Checked') return 'confirmed';
    if (check.status === 'Failed') return 'danger';
    return 'neutral';
  }

  money(value: number): string {
    return new Intl.NumberFormat('en-PK', {
      style: 'currency',
      currency: 'PKR',
      maximumFractionDigits: 0,
    }).format(value);
  }

  isSaved(id: number): boolean {
    return this.engagement.isSaved(id);
  }

  hasOpenVisit(id: number): boolean {
    return this.engagement.hasOpenVisit(id);
  }

  toggleSave(id: number): void {
    this.engagement.toggle(id, `/property/${id}`);
  }

  /**
   * Opens the thread for this listing and goes to it. Asking for the same thread twice is
   * a 200 with the one that already exists, not an error, so there is nothing to check
   * first - and the message page is where the conversation belongs, not a dialog here.
   */
  startThread(id: number): void {
    if (!this.auth.isAuthenticated()) {
      this.router.navigate(['/login'], { queryParams: { returnUrl: `/property/${id}` } });
      return;
    }
    if (!this.auth.isEmailVerified()) {
      this.router.navigate(['/verify']);
      return;
    }

    this.threadBusy.set(true);
    this.threadError.set(null);

    this.messaging.open(id).subscribe({
      next: (conversation) => {
        this.threadBusy.set(false);
        this.router.navigate(['/messages'], { queryParams: { c: conversation.id } });
      },
      error: (err) => {
        this.threadError.set(toFailure(err).message);
        this.threadBusy.set(false);
      },
    });
  }

  showPhoto(index: number): void {
    const count = this.photos().length;
    if (count === 0) return;
    this.activePhoto.set(((index % count) + count) % count);
  }
}
