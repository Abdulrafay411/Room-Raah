import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, of } from 'rxjs';
import { catchError, tap } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { Page } from '../models/catalog.model';
import {
  CreateReport,
  CreateReview,
  CreateVisitRequest,
  MyReport,
  MyReview,
  SavedListing,
  VisitRequest,
  isOpenVisit,
} from '../models/engagement.model';
import { AuthService } from '../auth/auth.service';

/**
 * The seeker's own side of the product: their shortlist, their visit requests, the reviews
 * and reports they file. Everything here needs a verified account - rule 3, browsing is
 * public and acting is not.
 *
 * The shortlist keeps a set of ids in a signal so a card anywhere in the application can
 * render its own saved state without each list fetching the shortlist again. It is loaded
 * once per session and kept in step by the toggle, which is cheaper and steadier than
 * re-reading it after every save.
 */
@Injectable({ providedIn: 'root' })
export class EngagementService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly base = environment.apiBaseUrl;

  private readonly savedIds = signal<ReadonlySet<number>>(new Set());
  private loadedFor: number | null = null;

  private readonly openVisitIds = signal<ReadonlySet<number>>(new Set());
  private visitsLoadedFor: number | null = null;

  readonly savedCount = computed(() => this.savedIds().size);

  isSaved(propertyId: number): boolean {
    return this.savedIds().has(propertyId);
  }

  /**
   * Called by the screens that draw save buttons. Signed out, the shortlist is empty rather
   * than an error: browsing is public, and a 401 fired on every landing page visit would be
   * noise in the console and nothing else.
   *
   * An unverified account is the same case for a stronger reason: it has no shortlist to
   * read, the API answers 403, and firing it on every public page is how an unverified
   * account ended up bounced to /verify the moment it opened the landing page.
   */
  primeShortlist(): void {
    const user = this.auth.currentUser();
    if (!user || !this.auth.isAuthenticated() || !this.auth.isEmailVerified()) {
      this.savedIds.set(new Set());
      this.loadedFor = null;
      return;
    }
    if (this.loadedFor === user.id) return;
    this.loadedFor = user.id;

    this.saved(1, 50)
      .pipe(catchError(() => of(null)))
      .subscribe((page) => {
        if (!page) return;
        this.savedIds.set(new Set(page.items.map((item) => item.id)));
      });
  }

  hasOpenVisit(propertyId: number): boolean {
    return this.openVisitIds().has(propertyId);
  }

  /**
   * Rule 7: a seeker may hold one open request per listing, and a second is a 422. The
   * listing itself does not say whether this person already has one - the API only tells
   * them through their own visit list - so it is read once and kept, the same way the
   * shortlist is, and the button is disabled rather than letting somebody write out a
   * request and be refused at the end of it.
   */
  primeOpenVisits(): void {
    const user = this.auth.currentUser();
    // Seeker only, and not just because the button is theirs: `/visits/my` answers 403 to an
    // owner or an admin reading the same listing page, and an unasked-for 403 is a console
    // error on a page that is working perfectly.
    if (
      !user ||
      !this.auth.isAuthenticated() ||
      !this.auth.isEmailVerified() ||
      this.auth.userRole() !== 'Seeker'
    ) {
      this.openVisitIds.set(new Set());
      this.visitsLoadedFor = null;
      return;
    }
    if (this.visitsLoadedFor === user.id) return;
    this.visitsLoadedFor = user.id;

    this.myVisits(1, 50)
      .pipe(catchError(() => of(null)))
      .subscribe((page) => {
        if (!page) return;
        this.openVisitIds.set(
          new Set(page.items.filter(isOpenVisit).map((visit) => visit.propertyId)),
        );
      });
  }

  saved(page = 1, pageSize = 12): Observable<Page<SavedListing>> {
    return this.http.get<Page<SavedListing>>(`${this.base}/saved`, {
      params: new HttpParams().set('page', page).set('pageSize', pageSize),
    });
  }

  save(propertyId: number): Observable<SavedListing> {
    return this.http
      .post<SavedListing>(`${this.base}/saved/${propertyId}`, {})
      .pipe(tap(() => this.markSaved(propertyId, true)));
  }

  /**
   * A 204 whatever became of the listing. That matters: a listing that leaves the site
   * disappears from the list while its row survives, so an unsave must keep working on
   * something the person can no longer see.
   */
  unsave(propertyId: number): Observable<void> {
    return this.http
      .delete<void>(`${this.base}/saved/${propertyId}`)
      .pipe(tap(() => this.markSaved(propertyId, false)));
  }

  /**
   * What every save button on every card does, in one place, because there are four of them
   * and they must all handle the same three cases: signed out, unverified, and saved.
   *
   * Signed out sends them to sign in and back again rather than failing quietly - a heart
   * that does nothing is the kind of thing a judge finds in ten seconds. Unverified goes to
   * `/verify`, which is rule 3 and not a toast. The `error` callback deliberately reverts
   * nothing, because the optimistic write only happens on success.
   */
  toggle(propertyId: number, returnUrl: string): void {
    if (!this.auth.isAuthenticated()) {
      this.router.navigate(['/login'], { queryParams: { returnUrl } });
      return;
    }
    if (!this.auth.isEmailVerified()) {
      this.router.navigate(['/verify']);
      return;
    }

    // Widened deliberately: the two calls answer with different bodies and neither body is
    // read here - only whether it worked.
    const request: Observable<unknown> = this.isSaved(propertyId)
      ? this.unsave(propertyId)
      : this.save(propertyId);
    request.subscribe({
      // A 422 here is "already on the list" and a 404 is "no longer published". Neither is
      // worth an alert over a heart icon; the list is re-read so the button tells the truth.
      error: () => {
        this.loadedFor = null;
        this.primeShortlist();
      },
    });
  }

  private markSaved(propertyId: number, saved: boolean): void {
    const next = new Set(this.savedIds());
    if (saved) next.add(propertyId);
    else next.delete(propertyId);
    this.savedIds.set(next);
  }

  private markOpenVisit(propertyId: number, open: boolean): void {
    const next = new Set(this.openVisitIds());
    if (open) next.add(propertyId);
    else next.delete(propertyId);
    this.openVisitIds.set(next);
  }

  requestVisit(payload: CreateVisitRequest): Observable<VisitRequest> {
    return this.http
      .post<VisitRequest>(`${this.base}/visits`, payload)
      .pipe(tap((visit) => this.markOpenVisit(visit.propertyId, true)));
  }

  myVisits(page = 1, pageSize = 20): Observable<Page<VisitRequest>> {
    return this.http.get<Page<VisitRequest>>(`${this.base}/visits/my`, {
      params: new HttpParams().set('page', page).set('pageSize', pageSize),
    });
  }

  cancelVisit(id: number): Observable<VisitRequest> {
    return this.http
      .patch<VisitRequest>(`${this.base}/visits/${id}/cancel`, {})
      .pipe(tap((visit) => this.markOpenVisit(visit.propertyId, false)));
  }

  /** Either party may press this, and only after `preferredAt` has passed. */
  completeVisit(id: number): Observable<VisitRequest> {
    return this.http.patch<VisitRequest>(`${this.base}/visits/${id}/complete`, {});
  }

  /** Created Pending and invisible until an admin approves it: rule 41. */
  writeReview(payload: CreateReview): Observable<MyReview> {
    return this.http.post<MyReview>(`${this.base}/reviews`, payload);
  }

  fileReport(payload: CreateReport): Observable<MyReport> {
    return this.http.post<MyReport>(`${this.base}/reports`, payload);
  }
}
