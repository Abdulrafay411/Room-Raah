import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map, switchMap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Page } from '../models/catalog.model';
import {
  ListingDraft,
  ListingWriteResponse,
  MyListing,
  MyListingSummary,
  OwnerInspectionState,
  unwrapWrite,
} from '../models/owner.model';
import { VisitRequest } from '../models/engagement.model';

/**
 * The owner's own listings, and the visit requests on them.
 *
 * Nothing here writes to a live listing. On something Published every one of these calls is
 * a proposal - the update, the photo add, the photo removal - and the server answers with a
 * pending revision rather than a changed listing. The screens are built around that; this
 * service just does not pretend otherwise.
 */
@Injectable({ providedIn: 'root' })
export class OwnerService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/owner/properties`;
  private readonly visitsUrl = `${environment.apiBaseUrl}/owner/visits`;

  list(page = 1, pageSize = 50): Observable<Page<MyListingSummary>> {
    return this.http.get<Page<MyListingSummary>>(this.base, {
      params: new HttpParams().set('page', page).set('pageSize', pageSize),
    });
  }

  detail(id: number): Observable<MyListing> {
    return this.http.get<MyListing>(`${this.base}/${id}`);
  }

  create(draft: ListingDraft): Observable<MyListing> {
    return this.http.post<MyListing>(this.base, draft);
  }

  /**
   * On a published listing this creates a revision and the live listing is untouched. Read
   * `pendingRevision` on the answer before choosing what to tell the owner - a listing can
   * be approved between the form loading and this returning.
   */
  update(id: number, draft: ListingDraft): Observable<MyListing> {
    return this.http.put<ListingWriteResponse>(`${this.base}/${id}`, draft).pipe(map(unwrapWrite));
  }

  /**
   * The one revision that is allowed to change nothing: re-confirming the same bed count is
   * the entire point of the button, and the server accepts it where a content revision that
   * changed nothing would be refused.
   */
  setAvailability(id: number, availableBeds: number): Observable<MyListing> {
    return this.http
      .patch<ListingWriteResponse>(`${this.base}/${id}/availability`, { availableBeds })
      .pipe(map(unwrapWrite));
  }

  submit(id: number): Observable<MyListing> {
    return this.http.post<MyListing>(`${this.base}/${id}/submit`, {});
  }

  /** Once per listing (rule 108). The answer says whether that one request is now spent. */
  requestInspection(id: number): Observable<OwnerInspectionState> {
    return this.http.post<OwnerInspectionState>(`${this.base}/${id}/inspection-request`, {});
  }

  /**
   * Multipart, field name `file`. The browser sets the boundary; do not set it here. The
   * answer is the one photograph, so the listing is read again to show it in place.
   */
  uploadPhoto(id: number, file: File): Observable<MyListing> {
    const body = new FormData();
    body.append('file', file, file.name);
    return this.http
      .post<unknown>(`${this.base}/${id}/photos`, body)
      .pipe(switchMap(() => this.detail(id)));
  }

  /** Answers 204 with nothing, so the listing is read again. */
  deletePhoto(id: number, photoId: number): Observable<MyListing> {
    return this.http
      .delete<void>(`${this.base}/${id}/photos/${photoId}`)
      .pipe(switchMap(() => this.detail(id)));
  }

  /** The list must name every photograph the listing holds, exactly once. */
  reorderPhotos(id: number, photoIds: number[]): Observable<MyListing> {
    return this.http
      .patch<ListingWriteResponse>(`${this.base}/${id}/photos/order`, { photoIds })
      .pipe(map(unwrapWrite));
  }

  makePrimary(id: number, photoId: number): Observable<MyListing> {
    return this.http
      .patch<ListingWriteResponse>(`${this.base}/${id}/photos/${photoId}/primary`, {})
      .pipe(map(unwrapWrite));
  }

  visits(page = 1, pageSize = 50): Observable<Page<VisitRequest>> {
    return this.http.get<Page<VisitRequest>>(this.visitsUrl, {
      params: new HttpParams().set('page', page).set('pageSize', pageSize),
    });
  }

  /** A decline must carry a note: rule 89, and a 400 without one. */
  respondToVisit(
    visitId: number,
    status: 'Accepted' | 'Declined',
    ownerResponseNote?: string,
  ): Observable<VisitRequest> {
    return this.http.patch<VisitRequest>(`${environment.apiBaseUrl}/visits/${visitId}/respond`, {
      status,
      ownerResponseNote: ownerResponseNote ?? null,
    });
  }

  completeVisit(visitId: number): Observable<VisitRequest> {
    return this.http.patch<VisitRequest>(`${environment.apiBaseUrl}/visits/${visitId}/complete`, {});
  }
}
