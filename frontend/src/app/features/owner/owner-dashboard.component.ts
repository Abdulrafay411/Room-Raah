import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import {
  BadgeComponent,
  ButtonComponent,
  ConfirmDialogComponent,
  ConfirmRequest,
  DialogComponent,
  EmptyStateComponent,
} from '../../shared/components';
import { OwnerService } from '../../core/services/owner.service';
import { toFailure } from '../../core/http/api-error';
import { VisitRequest, canComplete } from '../../core/models/engagement.model';
import {
  MyListing,
  MyListingSummary,
  canSubmit,
  nextStep,
  statusLabel,
  statusVariant,
  submitProblem,
} from '../../core/models/owner.model';

/**
 * Page 18. An owner's listings and the visit requests on them, on one screen.
 *
 * Two things this page is responsible for saying out loud. First, where each listing is in
 * the lifecycle and what the owner does about it from there - a submit button on something
 * already with the review team is a button that can only fail, and a badge that says
 * "Draft" without saying "add two more photographs" is a status report, not a next step.
 * Second, that confirming beds is a thing worth doing: it is the one revision the server
 * accepts unchanged, because re-confirming the same number is the entire point of it, so
 * the bed count sits on every live listing with its own small form.
 *
 * Visit requests are the time-sensitive half. On a laptop they sit beside the listings;
 * on a phone, under them, with a line at the top saying how many are waiting.
 */
@Component({
  selector: 'app-owner-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    BadgeComponent,
    ButtonComponent,
    EmptyStateComponent,
    ConfirmDialogComponent,
    DialogComponent,
  ],
  templateUrl: './owner-dashboard.component.html',
  styleUrls: ['./owner-dashboard.component.scss'],
})
export class OwnerDashboardComponent {
  private readonly owner = inject(OwnerService);
  private readonly router = inject(Router);

  readonly listings = signal<MyListingSummary[]>([]);
  readonly visits = signal<VisitRequest[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);
  readonly busyId = signal<number | null>(null);
  readonly confirm = signal<ConfirmRequest | null>(null);

  /** The bed count being typed per live listing, before it is confirmed. */
  readonly bedsDraft = signal<Record<number, number>>({});

  /** Rule 108: one inspection request per listing. Remembered here once it is spent. */
  readonly inspectionAsked = signal<Set<number>>(new Set());

  /** The decline note is required (rule 89), so declining is a dialog with the note in it. */
  readonly declining = signal<VisitRequest | null>(null);
  readonly declineNote = signal('');

  readonly showSettled = signal(false);

  readonly openVisits = computed(() => this.visits().filter((visit) => visit.status === 'Requested'));
  readonly upcomingVisits = computed(() => this.visits().filter((visit) => visit.status === 'Accepted'));
  readonly settledVisits = computed(() =>
    this.visits().filter((visit) => visit.status !== 'Requested' && visit.status !== 'Accepted'),
  );
  readonly liveCount = computed(() => this.listings().filter((l) => l.status === 'Published').length);

  readonly statusLabel = statusLabel;
  readonly statusVariant = statusVariant;
  readonly canSubmit = canSubmit;
  readonly canComplete = canComplete;
  readonly nextStep = nextStep;

  /**
   * The list shape carries a total rather than a live count. On a draft - the only state
   * where submit is offered - every photograph is live, so the two are the same number.
   */
  blockedBy(listing: MyListingSummary): string | null {
    return submitProblem(listing.status, listing.photoCount);
  }

  bedsFor(listing: MyListingSummary): number {
    return this.bedsDraft()[listing.id] ?? listing.availableBeds;
  }

  setBeds(listing: MyListingSummary, value: number): void {
    const beds = Math.max(0, Math.min(listing.totalBeds, Math.floor(Number(value) || 0)));
    this.bedsDraft.update((draft) => ({ ...draft, [listing.id]: beds }));
  }

  canAskInspection(listing: MyListingSummary): boolean {
    return (
      listing.status === 'Published' &&
      !listing.hasInspectionBadge &&
      !this.inspectionAsked().has(listing.id)
    );
  }

  constructor() {
    this.load();
  }

  addListing(): void {
    this.router.navigate(['/owner/listing/new']);
  }

  /** The line at the top of the page that says requests are waiting takes the reader to them. */
  goToVisits(): void {
    const heading = document.getElementById('owner-visits');
    heading?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    heading?.focus();
  }

  private load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.owner.list().subscribe({
      next: (page) => {
        this.listings.set(page.items ?? []);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(toFailure(err).message);
        this.loading.set(false);
      },
    });

    // The visits panel failing must not take the listings down with it: they are two
    // independent things that happen to share a screen.
    this.owner.visits().subscribe({
      next: (page) => this.visits.set(page.items ?? []),
      error: () => this.visits.set([]),
    });
  }

  /** Sending locks the listing until the review team answers, so it is worth one question. */
  askSubmit(listing: MyListingSummary): void {
    this.confirm.set({
      title: 'Send this listing for review?',
      description: `"${listing.title}" goes to the review team. Until they answer, it cannot be changed. If they approve it, it goes into search.`,
      confirmLabel: 'Send for review',
      variant: 'primary',
      run: () => {
        this.confirm.set(null);
        this.runOnListing(listing.id, this.owner.submit(listing.id), 'Sent for review. You will see the answer here.');
      },
    });
  }

  /**
   * Re-confirming the same bed count is accepted where a content revision that changed
   * nothing would be refused, so this is offered on every live listing whether or not the
   * number has moved.
   */
  confirmBeds(listing: MyListingSummary): void {
    const beds = this.bedsFor(listing);
    this.runOnListing(
      listing.id,
      this.owner.setAvailability(listing.id, beds),
      beds === listing.availableBeds
        ? 'Beds confirmed. Seekers see the listing is current.'
        : `Bed count sent: ${beds} free. It goes through the quick availability review, and the live number changes once that is done.`,
    );
  }

  /** Rule 108: once per listing, so it asks before it spends the one request. */
  askInspection(listing: MyListingSummary): void {
    this.confirm.set({
      title: 'Ask for an inspection?',
      description: `RoomRaah arranges a visit to "${listing.title}". If it passes, the listing carries the inspection badge in search. You can ask once per listing.`,
      confirmLabel: 'Ask for an inspection',
      variant: 'primary',
      run: () => this.requestInspection(listing),
    });
  }

  private requestInspection(listing: MyListingSummary): void {
    this.busyId.set(listing.id);
    this.error.set(null);
    this.notice.set(null);
    this.owner.requestInspection(listing.id).subscribe({
      next: () => {
        this.inspectionAsked.update((set) => new Set(set).add(listing.id));
        this.confirm.set(null);
        this.notice.set('Inspection requested. Somebody from RoomRaah will be in touch to arrange it.');
        this.busyId.set(null);
      },
      error: (err) => {
        this.confirm.set(null);
        this.error.set(toFailure(err).message);
        this.busyId.set(null);
      },
    });
  }

  /**
   * These endpoints answer with the full listing while this page holds summaries. Rather
   * than half-merging two different shapes, the affected row is re-read from the answer's
   * fields that the summary actually has.
   */
  private runOnListing(id: number, request: Observable<MyListing>, done: string): void {
    this.busyId.set(id);
    this.error.set(null);
    this.notice.set(null);

    request.subscribe({
      next: (updated) => {
        this.listings.update((list) => list.map((item) => (item.id === id ? merge(item, updated) : item)));
        this.bedsDraft.update((draft) => {
          const { [id]: _dropped, ...rest } = draft;
          return rest;
        });
        this.notice.set(done);
        this.busyId.set(null);
      },
      error: (err) => {
        // A 422 here is a sentence explaining exactly which rule was hit - not enough
        // photographs, wrong status - so it goes in front of the owner as written.
        this.error.set(toFailure(err).message);
        this.busyId.set(null);
      },
    });
  }

  accept(visit: VisitRequest): void {
    this.respond(visit.id, this.owner.respondToVisit(visit.id, 'Accepted'), 'Visit accepted. The seeker has been told.');
  }

  startDecline(visit: VisitRequest): void {
    this.declineNote.set('');
    this.declining.set(visit);
  }

  cancelDecline(): void {
    this.declining.set(null);
    this.declineNote.set('');
  }

  /** Rule 89: a decline carries a note, and the server answers 400 without one. */
  confirmDecline(): void {
    const visit = this.declining();
    const note = this.declineNote().trim();
    if (!visit) return;
    if (!note) {
      this.error.set('Say why you are declining. The seeker reads it.');
      return;
    }
    this.respond(visit.id, this.owner.respondToVisit(visit.id, 'Declined', note), 'Visit declined. The seeker has your note.');
    this.declining.set(null);
  }

  completeVisit(visit: VisitRequest): void {
    this.respond(visit.id, this.owner.completeVisit(visit.id), 'Visit marked as done. The seeker can now write a review.');
  }

  private respond(id: number, request: Observable<VisitRequest>, done: string): void {
    this.busyId.set(id);
    this.error.set(null);
    this.notice.set(null);

    request.subscribe({
      next: (updated) => {
        this.visits.update((list) => list.map((v) => (v.id === updated.id ? updated : v)));
        this.notice.set(done);
        this.busyId.set(null);
      },
      error: (err) => {
        this.error.set(toFailure(err).message);
        this.busyId.set(null);
      },
    });
  }
}

/** The fields a summary row shows that a full listing also answers with. */
function merge(row: MyListingSummary, updated: MyListing): MyListingSummary {
  return {
    ...row,
    title: updated.title,
    status: updated.status,
    areaName: updated.areaName,
    cityName: updated.cityName,
    monthlyRent: updated.monthlyRent,
    availableBeds: updated.availableBeds,
    totalBeds: updated.totalBeds,
    photoCount: updated.photos.length,
    hasInspectionBadge: updated.hasInspectionBadge,
    availabilityConfirmedAt: updated.availabilityConfirmedAt,
    updatedAt: updated.updatedAt,
  };
}
