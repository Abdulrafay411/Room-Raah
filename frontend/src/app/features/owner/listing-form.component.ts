import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import {
  BadgeComponent,
  ButtonComponent,
  FormFieldComponent,
  InputDirective,
} from '../../shared/components';
// By path rather than through the barrel: the barrel reaches the app shell, and Leaflet
// belongs in this screen's lazy chunk.
import { MapPinComponent } from '../../shared/components/map-pin/map-pin.component';
import { OwnerService } from '../../core/services/owner.service';
import { ReferenceService } from '../../core/services/reference.service';
import { fieldErrorMap, toFailure } from '../../core/http/api-error';
import {
  Area,
  City,
  Facility,
  GENDER_POLICIES,
  GenderPolicy,
  ROOM_TYPES,
  RoomType,
} from '../../core/models/catalog.model';
import {
  ListingDraft,
  ListingPhoto,
  MyListing,
  PHOTOS_MAX,
  PHOTOS_MIN,
  emptyDraft,
  livePhotoCount,
  photoProblem,
  saveEffect,
  saveLabel,
  saveOutcome,
  statusLabel,
  statusVariant,
  submitProblem,
  toDraft,
} from '../../core/models/owner.model';

/**
 * Page 19: add or edit a listing. The fiddliest screen in the product, and the one where
 * getting the copy wrong actively misleads somebody.
 *
 * The rule underneath all of it: an owner never edits a published listing directly. A save
 * on something Published creates a revision, the live listing does not move, and the button
 * has to say "Send changes for review" rather than "Save" - before it is pressed, not after.
 * `PendingReview` is refused outright because an admin is reading it right now, so the form
 * is read-only there instead of offering a button that can only 422.
 *
 * Photographs behave the same way. On a published listing an upload is a proposal, and the
 * new photo comes back marked `PendingAdd` rather than `Live`. Only live ones count towards
 * the three a submission needs, which is why the counter here counts those and not `length`.
 */
@Component({
  selector: 'app-listing-form',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    BadgeComponent,
    ButtonComponent,
    FormFieldComponent,
    InputDirective,
    MapPinComponent,
  ],
  templateUrl: './listing-form.component.html',
  styleUrls: ['./listing-form.component.scss'],
})
export class ListingFormComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly owner = inject(OwnerService);
  private readonly reference = inject(ReferenceService);

  readonly listing = signal<MyListing | null>(null);
  readonly draft = signal<ListingDraft>(emptyDraft());
  readonly loading = signal(true);
  readonly saving = signal(false);

  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);
  readonly fieldErrors = signal<Record<string, string>>({});

  readonly cities = signal<City[]>([]);
  readonly areas = signal<Area[]>([]);
  readonly facilities = signal<Facility[]>([]);
  readonly cityId = signal<number | null>(null);

  readonly photoBusy = signal(false);
  readonly photoError = signal<string | null>(null);

  readonly roomTypes = ROOM_TYPES;
  readonly genderPolicies = GENDER_POLICIES;
  readonly photosMin = PHOTOS_MIN;
  readonly photosMax = PHOTOS_MAX;

  readonly statusLabel = statusLabel;
  readonly statusVariant = statusVariant;

  readonly isNew = computed(() => this.listing() === null);

  /** Read-only while an admin has it: a save from here is a 422 and nothing else. */
  readonly locked = computed(() => {
    const listing = this.listing();
    return listing ? saveEffect(listing.status) === 'locked' : false;
  });

  readonly makesRevision = computed(() => {
    const listing = this.listing();
    return listing ? saveEffect(listing.status) === 'revision' : false;
  });

  readonly saveLabel = computed(() => {
    const listing = this.listing();
    return listing ? saveLabel(listing.status) : 'Create listing';
  });

  readonly photos = computed(() => this.listing()?.photos ?? []);
  readonly liveCount = computed(() => {
    const listing = this.listing();
    return listing ? livePhotoCount(listing) : 0;
  });

  readonly submitBlockedBy = computed(() => {
    const listing = this.listing();
    return listing ? submitProblem(listing.status, livePhotoCount(listing)) : null;
  });

  constructor() {
    this.reference.cities().subscribe((list) => this.cities.set(list ?? []));
    this.reference.facilities().subscribe((list) => this.facilities.set(list ?? []));

    this.route.paramMap.subscribe((params) => {
      const raw = params.get('id');
      if (!raw || raw === 'new') {
        this.listing.set(null);
        this.draft.set(emptyDraft());
        this.loading.set(false);
        return;
      }
      this.load(Number(raw));
    });
  }

  private load(id: number): void {
    if (!Number.isFinite(id) || id <= 0) {
      this.loading.set(false);
      this.error.set('That listing does not exist.');
      return;
    }

    this.owner.detail(id).subscribe({
      next: (listing) => {
        this.adopt(listing);
        this.cityId.set(listing.cityId);
        this.loadAreas(listing.cityId);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(toFailure(err).message);
        this.loading.set(false);
      },
    });
  }

  /**
   * The server's answer replaces the form's state wholesale. That matters after a revision:
   * the fields must go back to showing the *live* listing, because that is what the reader
   * of the page owns - the changes are elsewhere, waiting.
   */
  private adopt(listing: MyListing): void {
    this.listing.set(listing);
    this.draft.set(toDraft(listing));
  }

  onCity(cityId: number | null): void {
    this.cityId.set(cityId);
    this.patch({ areaId: 0 });
    this.loadAreas(cityId);
  }

  private loadAreas(cityId: number | null): void {
    if (!cityId) {
      this.areas.set([]);
      return;
    }
    this.reference.areas(cityId).subscribe((list) => this.areas.set(list ?? []));
  }

  patch(change: Partial<ListingDraft>): void {
    this.draft.update((current) => ({ ...current, ...change }));
  }

  toggleFacility(id: number, on: boolean): void {
    const ids = new Set(this.draft().facilityIds);
    if (on) ids.add(id);
    else ids.delete(id);
    this.patch({ facilityIds: [...ids] });
  }

  hasFacility(id: number): boolean {
    return this.draft().facilityIds.includes(id);
  }

  onPin(at: { latitude: number; longitude: number }): void {
    this.patch(at);
  }

  /**
   * The same three rules the server enforces, checked here so somebody is told before they
   * submit rather than after. Never only here - the server is still the authority.
   */
  private localProblem(): string | null {
    const draft = this.draft();
    if (!draft.title.trim()) return 'Give the listing a title.';
    if (!draft.description.trim()) return 'Describe the room.';
    if (!draft.addressLine.trim()) return 'Add the address.';
    if (!draft.areaId) return 'Choose the area the room is in.';
    if (draft.totalBeds < 1) return 'A room has at least one bed.';
    if (draft.availableBeds > draft.totalBeds) {
      return 'There cannot be more free beds than beds.';
    }
    if (draft.monthlyRent <= 0) return 'Set a monthly rent.';
    return null;
  }

  save(): void {
    const problem = this.localProblem();
    if (problem) {
      this.error.set(problem);
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    this.notice.set(null);
    this.fieldErrors.set({});

    const listing = this.listing();
    const request = listing
      ? this.owner.update(listing.id, this.draft())
      : this.owner.create(this.draft());

    request.subscribe({
      next: (saved) => {
        this.adopt(saved);
        this.saving.set(false);

        if (!listing) {
          // A new listing gets its own URL, so a refresh does not land back on a blank form.
          this.notice.set('Created. Add photographs, then send it for review.');
          this.router.navigate(['/owner/listing', saved.id], { replaceUrl: true });
          return;
        }
        // `pendingRevision` on the response is the authority, not the status we sent with it.
        this.notice.set(saveOutcome(saved));
      },
      error: (err) => {
        const failure = toFailure(err);
        this.error.set(failure.message);
        this.fieldErrors.set(fieldErrorMap(failure));
        this.saving.set(false);
      },
    });
  }

  submitForReview(): void {
    const listing = this.listing();
    if (!listing) return;
    this.runPhoto(this.owner.submit(listing.id), 'Sent for review.');
  }

  onFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    const problem = photoProblem(file);
    if (problem) {
      this.photoError.set(problem);
      input.value = '';
      return;
    }

    const listing = this.listing();
    if (!listing) return;

    this.photoError.set(null);
    this.runPhoto(
      this.owner.uploadPhoto(listing.id, file),
      this.makesRevision()
        ? 'Photograph sent for review — it is not on the live listing yet.'
        : 'Photograph added.',
    );
    input.value = '';
  }

  removePhoto(photo: ListingPhoto): void {
    const listing = this.listing();
    if (!listing) return;
    this.runPhoto(
      this.owner.deletePhoto(listing.id, photo.id),
      this.makesRevision()
        ? 'Removal sent for review — the photograph is still on the live listing.'
        : 'Photograph removed.',
    );
  }

  makePrimary(photo: ListingPhoto): void {
    const listing = this.listing();
    if (!listing) return;
    this.runPhoto(this.owner.makePrimary(listing.id, photo.id), 'Main photograph changed.');
  }

  /** Reordering sends every photograph the listing holds, exactly once, in the new order. */
  movePhoto(photo: ListingPhoto, by: number): void {
    const listing = this.listing();
    if (!listing) return;

    const ids = [...this.photos()]
      .sort((a, b) => a.sortOrder - b.sortOrder)
      .map((item) => item.id);
    const from = ids.indexOf(photo.id);
    const to = from + by;
    if (from < 0 || to < 0 || to >= ids.length) return;

    ids.splice(to, 0, ids.splice(from, 1)[0]);
    this.runPhoto(this.owner.reorderPhotos(listing.id, ids), 'Order changed.');
  }

  private runPhoto(request: Observable<MyListing>, done: string): void {
    this.photoBusy.set(true);
    this.photoError.set(null);
    this.notice.set(null);

    request.subscribe({
      next: (updated) => {
        this.adopt(updated);
        this.notice.set(done);
        this.photoBusy.set(false);
      },
      error: (err) => {
        this.photoError.set(toFailure(err).message);
        this.photoBusy.set(false);
      },
    });
  }
}
