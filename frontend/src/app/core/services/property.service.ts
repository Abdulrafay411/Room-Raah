import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  Page,
  PropertyCard,
  PropertyDetail,
  PropertyFilters,
  PropertyPhoto,
  ReviewPage,
} from '../models/catalog.model';

/** At most three, per rule 11 of the brief's section 10. A fourth is a 422. */
export const COMPARE_LIMIT = 3;

/**
 * The public catalogue. Everything here is anonymous - browsing needs no account, which is
 * rule 3, and is why none of these methods touch the session.
 */
@Injectable({ providedIn: 'root' })
export class PropertyService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBaseUrl;

  search(filters: PropertyFilters): Observable<Page<PropertyCard>> {
    return this.http.get<Page<PropertyCard>>(`${this.base}/properties`, {
      params: toParams(filters),
    });
  }

  featured(): Observable<PropertyCard[]> {
    return this.http.get<PropertyCard[]>(`${this.base}/properties/featured`);
  }

  detail(id: number): Observable<PropertyDetail> {
    return this.http.get<PropertyDetail>(`${this.base}/properties/${id}`);
  }

  photos(id: number): Observable<PropertyPhoto[]> {
    return this.http.get<PropertyPhoto[]>(`${this.base}/properties/${id}/photos`);
  }

  reviews(id: number, page = 1, pageSize = 12): Observable<ReviewPage> {
    return this.http.get<ReviewPage>(`${this.base}/properties/${id}/reviews`, {
      params: new HttpParams().set('page', page).set('pageSize', pageSize),
    });
  }

  /**
   * Duplicates are removed and the list is capped before it leaves, because the fourth id
   * is a 422 and the interface is supposed to have stopped that happening. An id that
   * resolves to nothing is quietly left out by the server rather than refusing the lot.
   */
  compare(ids: number[], landmarkId?: number): Observable<PropertyCard[]> {
    const wanted = [...new Set(ids)].slice(0, COMPARE_LIMIT);
    let params = new HttpParams().set('ids', wanted.join(','));
    // The Distance row is otherwise a permanent dash: the server only measures against a
    // place, and the person carried one here from the search they were comparing out of.
    if (landmarkId != null) params = params.set('landmarkId', landmarkId);
    return this.http.get<PropertyCard[]>(`${this.base}/properties/compare`, { params });
  }
}

/**
 * Only what was actually set is sent. An empty string is not the same as an absent filter:
 * `cityId=` is a 400, and a filter the person cleared should read as "no opinion", not as
 * "match nothing".
 */
export function toParams(filters: PropertyFilters): HttpParams {
  let params = new HttpParams();

  for (const [key, value] of Object.entries(filters)) {
    if (value === undefined || value === null || value === '') continue;

    if (Array.isArray(value)) {
      if (value.length === 0) continue;
      params = params.set(key, value.join(','));
      continue;
    }
    params = params.set(key, String(value));
  }

  return params;
}
