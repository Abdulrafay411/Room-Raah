import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { BadgeComponent, ButtonComponent, EmptyStateComponent } from '../../shared/components';
import { OwnerService } from '../../core/services/owner.service';
import { toFailure } from '../../core/http/api-error';
import { VisitRequest, canComplete } from '../../core/models/engagement.model';
import {
  MyListing,
  MyListingSummary,
  PropertyStatus,
  canSubmit,
  statusLabel,
  statusVariant,
  submitProblem,
} from '../../core/models/owner.model';

/**
 * Page 18. An owner's listings and the visit requests on them, on one screen.
 *
 * Two things this page is responsible for saying out loud. First, where each listing is in
 * the lifecycle and what the owner can do about it from there - a submit button on something
 * already with the review team is a button that can only fail. Second, that confirming beds
 * is a thing worth doing: it is the one revision the server accepts unchanged, because
 * re-confirming the same number is the entire point of it.
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
  ],
  templateUrl: './owner-dashboard.component.html',
  styleUrls: ['./owner-dashboard.component.scss'],
})
export class OwnerDashboardComponent {
  private readonly owner = inject(OwnerService);

  readonly listings = signal<MyListingSummary[]>([]);
  readonly visits = signal<VisitRequest[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);
  readonly busyId = signal<number | null>(null);

  /** The decline note is required, so the form for it opens against one request at a time. */
  readonly decliningId = signal<number | null>(null);
  readonly declineNote = signal('');

  readonly openVisits = computed(() =>
    this.visits().filter((visit) => visit.status === 'Requested'),
  );
  readonly settledVisits = computed(() =>
    this.visits().filter((visit) => visit.status !== 'Requested'),
  );

  readonly statusLabel = statusLabel;
  readonly statusVariant = statusVariant;
  readonly canSubmit = canSubmit;
  readonly canComplete = canComplete;

  /**
   * The list shape carries a total rather than a live count. On a draft - the only state
   * where submit is offered - every photograph is live, so the two are the same number.
   */
  blockedBy(listing: MyListingSummary): string | null {
    return submitProblem(listing.status, listing.photoCount);
  }

  constructor() {
    this.load();
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

  submit(listing: MyListingSummary): void {
    this.runOnListing(listing.id, this.owner.submit(listing.id), 'Sent for review.');
  }

  /**
   * Re-confirming the same bed count is accepted where a content revision that changed
   * nothing would be refused, so this is offered on every listing rather than only when the
   * number has moved.
   */
  confirmBeds(listing: MyListingSummary): void {
    this.runOnListing(
      listing.id,
      this.owner.setAvailability(listing.id, listing.availableBeds),
      'Availability confirmed.',
    );
  }

  requestInspection(listing: MyListingSummary): void {
    this.busyId.set(listing.id);
    this.notice.set(null);
    this.owner.requestInspection(listing.id).subscribe({
      next: () => {
        this.notice.set('Inspection requested. Somebody will be in touch to arrange it.');
        this.busyId.set(null);
      },
      error: (err) => {
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
        this.listings.update((list) =>
          list.map((item) => (item.id === id ? merge(item, updated) : item)),
        );
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
    this.respond(visit.id, this.owner.respondToVisit(visit.id, 'Accepted'));
  }

  startDecline(visit: VisitRequest): void {
    this.decliningId.set(visit.id);
    this.declineNote.set('');
  }

  cancelDecline(): void {
    this.decliningId.set(null);
    this.declineNote.set('');
  }

  /** Rule 89: a decline carries a note, and the server answers 400 without one. */
  confirmDecline(visit: VisitRequest): void {
    const note = this.declineNote().trim();
    if (!note) {
      this.error.set('Say why you are declining — the seeker sees this.');
      return;
    }
    this.respond(visit.id, this.owner.respondToVisit(visit.id, 'Declined', note));
    this.decliningId.set(null);
  }

  completeVisit(visit: VisitRequest): void {
    this.respond(visit.id, this.owner.completeVisit(visit.id));
  }

  private respond(id: number, request: Observable<VisitRequest>): void {
    this.busyId.set(id);
    this.error.set(null);

    request.subscribe({
      next: (updated) => {
        this.visits.update((list) => list.map((v) => (v.id === updated.id ? updated : v)));
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
