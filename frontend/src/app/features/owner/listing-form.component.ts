import { Component, DestroyRef, computed, effect, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import {
  BadgeComponent,
  ButtonComponent,
  ConfirmDialogComponent,
  ConfirmRequest,
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
  PendingRevision,
  emptyDraft,
  isDirty,
  livePhotoCount,
  nextStep,
  photoProblem,
  readiness,
  revisionFieldLabel,
  revisionSince,
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
 * The page has two different kinds of write and used to put the button for each at a
 * different end of a long scroll. The form's own save lives in a bar that stays at the
 * bottom of the screen, and it only lights up once something has changed, because a
 * revision that changes nothing is refused. Sending the whole listing for review is a
 * separate decision, so it sits beside the form with the checklist of what it still needs.
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
    ConfirmDialogComponent,
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
  readonly confirm = signal<ConfirmRequest | null>(null);

  /**
   * The revision the last save made, with its diff. The listing itself does not carry one
   * when it is read back, so this is the one place an owner sees what they proposed beside
   * what is live, and it lasts as long as this page does.
   */
  readonly revision = signal<PendingRevision | null>(null);

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
  readonly fieldLabel = revisionFieldLabel;
  readonly revisionSince = revisionSince;

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

  /** Whether anything on the form differs from the listing it was loaded from. */
  readonly dirty = computed(() => {
    const listing = this.listing();
    return listing ? isDirty(this.draft(), toDraft(listing)) : true;
  });

  readonly photos = computed(() =>
    [...(this.listing()?.photos ?? [])].sort((a, b) => a.sortOrder - b.sortOrder),
  );
  readonly liveCount = computed(() => {
    const listing = this.listing();
    return listing ? livePhotoCount(listing) : 0;
  });

  readonly canSubmit = computed(() => {
    const listing = this.listing();
    return !!listing && (listing.status === 'Draft' || listing.status === 'ChangesRequested');
  });

  readonly checklist = computed(() => readiness(this.draft(), this.liveCount()));
  readonly checklistDone = computed(() => this.checklist().filter((item) => item.done).length);

  readonly submitBlockedBy = computed(() => {
    const listing = this.listing();
    return listing ? submitProblem(listing.status, livePhotoCount(listing)) : null;
  });

  readonly next = computed(() => {
    const listing = this.listing();
    return listing ? nextStep(listing.status, livePhotoCount(listing)) : null;
  });

  constructor() {
    // The save bar is fixed to the bottom of the screen, where the assistant's launcher
    // also lives. The body says a bar is there and the launcher moves up (styles.scss).
    const destroyRef = inject(DestroyRef);
    effect(() => {
      document.body.classList.toggle('has-bottom-bar', !this.loading() && !this.locked());
    });
    destroyRef.onDestroy(() => document.body.classList.remove('has-bottom-bar'));

    this.reference.cities().subscribe((list) => this.cities.set(list ?? []));
    this.reference.facilities().subscribe((list) => this.facilities.set(list ?? []));

    this.route.paramMap.subscribe((params) => {
      const raw = params.get('id');
      this.revision.set(null);
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
    if (listing.pendingRevision) this.revision.set(listing.pendingRevision);
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
    // A field that was wrong and has been touched is no longer shouting.
    const key = Object.keys(change)[0];
    if (key && this.fieldErrors()[key]) {
      this.fieldErrors.update((errors) => {
        const { [key]: _cleared, ...rest } = errors;
        return rest;
      });
    }
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

  discard(): void {
    const listing = this.listing();
    if (!listing) return;
    this.draft.set(toDraft(listing));
    this.fieldErrors.set({});
    this.error.set(null);
  }

  /**
   * The same rules the server enforces, checked here so somebody is told before they
   * submit rather than after, and at the field rather than only at the top. Never only
   * here - the server is still the authority.
   */
  private localProblems(): Record<string, string> {
    const draft = this.draft();
    const problems: Record<string, string> = {};
    if (!draft.title.trim()) problems['title'] = 'Give the listing a title.';
    if (!draft.description.trim()) problems['description'] = 'Describe the room.';
    if (!draft.areaId) problems['areaId'] = 'Choose the area the room is in.';
    if (!draft.addressLine.trim()) problems['addressLine'] = 'Add the address.';
    if (draft.totalBeds < 1) problems['totalBeds'] = 'A room has at least one bed.';
    if (draft.availableBeds > draft.totalBeds) {
      problems['availableBeds'] = 'There cannot be more free beds than beds.';
    }
    if (draft.monthlyRent <= 0) problems['monthlyRent'] = 'Set a monthly rent.';
    return problems;
  }

  save(): void {
    const problems = this.localProblems();
    const first = Object.keys(problems)[0];
    if (first) {
      this.fieldErrors.set(problems);
      this.error.set('Some fields need attention before this can be saved.');
      focusField(first);
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
          this.notice.set('Created. Add at least three photographs, then send it for review.');
          this.router.navigate(['/owner/listing', saved.id], { replaceUrl: true });
          return;
        }
        // `pendingRevision` on the response is the authority, not the status we sent with it.
        this.notice.set(saveOutcome(saved));
      },
      error: (err) => {
        const failure = toFailure(err);
        this.error.set(failure.message);
        const errors = fieldErrorMap(failure);
        this.fieldErrors.set(errors);
        const firstServer = Object.keys(errors)[0];
        if (firstServer) focusField(firstServer);
        this.saving.set(false);
      },
    });
  }

  /** Sending locks the listing until the review team answers, so it is worth one question. */
  askSubmit(): void {
    const listing = this.listing();
    if (!listing) return;
    this.confirm.set({
      title: 'Send this listing for review?',
      description: `"${listing.title}" goes to the review team. Until they answer, it cannot be changed. If they approve it, it goes into search.`,
      confirmLabel: 'Send for review',
      variant: 'primary',
      run: () => {
        this.confirm.set(null);
        this.runPhoto(this.owner.submit(listing.id), 'Sent for review. You will see the answer on your listings page.');
      },
    });
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
        ? 'Photograph sent for review. It is not on the live listing yet.'
        : 'Photograph added.',
    );
    input.value = '';
  }

  /** Removing is final on a draft and a proposal on a live listing; either way it asks. */
  askRemovePhoto(photo: ListingPhoto): void {
    const listing = this.listing();
    if (!listing) return;
    const revision = this.makesRevision();
    this.confirm.set({
      title: revision ? 'Propose removing this photograph?' : 'Remove this photograph?',
      description: revision
        ? 'It stays on the live listing until the review team approves the removal.'
        : photo.isPrimary
          ? 'It is the main photograph. The next one in order becomes the main one.'
          : 'It is removed from the listing straight away.',
      confirmLabel: revision ? 'Propose removal' : 'Remove photograph',
      run: () => {
        this.confirm.set(null);
        this.runPhoto(
          this.owner.deletePhoto(listing.id, photo.id),
          revision
            ? 'Removal sent for review. The photograph is still on the live listing.'
            : 'Photograph removed.',
        );
      },
    });
  }

  makePrimary(photo: ListingPhoto): void {
    const listing = this.listing();
    if (!listing) return;
    this.runPhoto(this.owner.makePrimary(listing.id, photo.id), 'Main photograph changed. It is the one every card shows.');
  }

  /** Reordering sends every photograph the listing holds, exactly once, in the new order. */
  movePhoto(photo: ListingPhoto, by: number): void {
    const listing = this.listing();
    if (!listing) return;

    const ids = this.photos().map((item) => item.id);
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

/** The field ids are the draft's keys, so a problem can be taken straight to its input. */
function focusField(key: string): void {
  setTimeout(() => {
    const element = document.getElementById(key);
    if (!element) return;
    element.scrollIntoView({ behavior: 'smooth', block: 'center' });
    element.focus({ preventScroll: true });
  }, 0);
}
