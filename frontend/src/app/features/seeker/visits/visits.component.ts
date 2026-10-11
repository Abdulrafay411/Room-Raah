import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Observable, forkJoin, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { BadgeComponent, ButtonComponent, EmptyStateComponent } from '../../../shared/components';
import { EngagementService } from '../../../core/services/engagement.service';
import { PropertyService } from '../../../core/services/property.service';
import { toFailure } from '../../../core/http/api-error';
import {
  VisitRequest,
  VisitStatus,
  canCancel,
  canComplete,
  canReview,
  directionsUrl,
  wantsDirections,
} from '../../../core/models/engagement.model';

/**
 * The seeker's own visit requests.
 *
 * This page exists because of one sentence in the brief: completing a visit is what unlocks
 * writing a review, and if the interface never surfaces the complete button then nobody can
 * ever review anything. Page 18 gives an owner their side of this; without this page the
 * seeker has no side at all and a whole feature is unreachable.
 *
 * The buttons follow the state machine rather than guessing - cancel from Requested or
 * Accepted, complete from Accepted and only once the time has passed, review once Completed.
 * A button that can only produce a 422 is worse than no button.
 */
@Component({
  selector: 'app-visits',
  standalone: true,
  imports: [CommonModule, RouterLink, BadgeComponent, ButtonComponent, EmptyStateComponent],
  templateUrl: './visits.component.html',
  styleUrls: ['./visits.component.scss'],
})
export class VisitsComponent {
  private readonly engagement = inject(EngagementService);
  private readonly properties = inject(PropertyService);

  readonly visits = signal<VisitRequest[]>([]);
  /**
   * Directions links by listing id. A visit request carries no coordinates, so they come
   * from the public listing - fetched only for the visits that will show the link.
   */
  private readonly directions = signal<ReadonlyMap<number, string>>(new Map());
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  /** What the last action refused with, shown verbatim: a 422 here is written to be read. */
  readonly actionError = signal<string | null>(null);
  readonly busyId = signal<number | null>(null);

  readonly openCount = computed(() => this.visits().filter((visit) => canCancel(visit)).length);

  readonly canCancel = canCancel;
  readonly canComplete = canComplete;
  readonly canReview = canReview;

  constructor() {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.engagement.myVisits(1, 50).subscribe({
      next: (page) => {
        this.visits.set(page.items ?? []);
        this.loading.set(false);
        this.loadDirections(this.visits());
      },
      error: (err) => {
        this.error.set(toFailure(err).message);
        this.loading.set(false);
      },
    });
  }

  /**
   * One listing request per accepted in-person visit, each allowed to fail on its own: a
   * listing taken off the site answers 404, and that should cost its own link, not the page.
   */
  private loadDirections(visits: VisitRequest[]): void {
    const ids = [...new Set(visits.filter(wantsDirections).map((visit) => visit.propertyId))];
    if (ids.length === 0) return;

    forkJoin(
      ids.map((id) =>
        this.properties.detail(id).pipe(
          map((listing) => [id, directionsUrl(listing.latitude, listing.longitude)] as const),
          catchError(() => of([id, null] as const)),
        ),
      ),
    ).subscribe((pairs) => {
      const found = new Map<number, string>();
      for (const [id, url] of pairs) if (url) found.set(id, url);
      this.directions.set(found);
    });
  }

  /** The link for this visit, or null when it should not have one or none could be built. */
  directionsFor(visit: VisitRequest): string | null {
    return wantsDirections(visit) ? (this.directions().get(visit.propertyId) ?? null) : null;
  }

  cancel(visit: VisitRequest): void {
    this.act(visit.id, this.engagement.cancelVisit(visit.id));
  }

  complete(visit: VisitRequest): void {
    this.act(visit.id, this.engagement.completeVisit(visit.id));
  }

  private act(id: number, request: Observable<VisitRequest>): void {
    this.busyId.set(id);
    this.actionError.set(null);

    request.subscribe({
      // The server sends the whole request back in its new state, so the row is replaced
      // with the server's answer rather than with a guess about what that answer would be.
      next: (updated) => {
        this.visits.update((list) => list.map((v) => (v.id === updated.id ? updated : v)));
        this.busyId.set(null);
      },
      error: (err) => {
        this.actionError.set(toFailure(err).message);
        this.busyId.set(null);
      },
    });
  }

  statusVariant(status: VisitStatus): 'confirmed' | 'warning' | 'danger' | 'neutral' {
    if (status === 'Accepted' || status === 'Completed') return 'confirmed';
    if (status === 'Requested') return 'warning';
    if (status === 'Declined' || status === 'Cancelled') return 'danger';
    return 'neutral';
  }

  /**
   * Why an accepted request has no complete button yet. Saying "once the time has passed"
   * is the difference between a button that is waiting and a button that is broken.
   */
  waitingNote(visit: VisitRequest): string | null {
    if (visit.status !== 'Accepted' || canComplete(visit)) return null;
    return 'You can mark this done once the visit time has passed.';
  }
}
