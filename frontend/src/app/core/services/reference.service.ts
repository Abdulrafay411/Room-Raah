import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, of } from 'rxjs';
import { catchError, map, shareReplay } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { Area, City, Facility, LocationSuggestion } from '../models/catalog.model';

/**
 * Cities, areas and facilities - the lists the filters are built from.
 *
 * These never change during a session, and the filter panel, the listing form and the
 * search page all want them at once. Each is fetched once and shared: `shareReplay` keeps
 * the answer, so a second subscriber gets the cached list instead of a second request.
 * Areas are cached per city for the same reason - opening the same city twice is common.
 */
@Injectable({ providedIn: 'root' })
export class ReferenceService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBaseUrl;

  private cities$?: Observable<City[]>;
  private readonly areasByCity = new Map<number, Observable<Area[]>>();
  private facilities$?: Observable<Facility[]>;

  cities(): Observable<City[]> {
    this.cities$ ??= this.http
      .get<City[]>(`${this.base}/locations/cities`)
      .pipe(shareReplay({ bufferSize: 1, refCount: false }));
    return this.cities$;
  }

  areas(cityId: number): Observable<Area[]> {
    let cached = this.areasByCity.get(cityId);
    if (!cached) {
      cached = this.http.get<Area[]>(`${this.base}/locations/cities/${cityId}/areas`).pipe(
        // A city id out of a hand-edited URL is a 404 here. The areas list is one filter
        // control, not the page: losing it should cost that control, not throw an
        // unhandled error across the search screen. The invalid city still gets reported,
        // by the 422 the search itself comes back with.
        catchError(() => of([] as Area[])),
        shareReplay({ bufferSize: 1, refCount: false }),
      );
      this.areasByCity.set(cityId, cached);
    }
    return cached;
  }

  facilities(): Observable<Facility[]> {
    this.facilities$ ??= this.http
      .get<Facility[]>(`${this.base}/facilities`)
      .pipe(shareReplay({ bufferSize: 1, refCount: false }));
    return this.facilities$;
  }

  /**
   * Type-ahead over cities, areas and landmarks in one list.
   *
   * The server takes 2 to 60 characters; anything shorter is answered here with an empty
   * list rather than a 400, because a person typing the first letter of a city has not
   * made a mistake. A failed suggest also answers empty - a dead type-ahead should not put
   * an error banner over a search box somebody is still using.
   */
  suggest(query: string): Observable<LocationSuggestion[]> {
    const trimmed = query.trim();
    if (trimmed.length < 2) return of([]);

    return this.http
      .get<LocationSuggestion[]>(`${this.base}/locations/suggest`, {
        params: new HttpParams().set('q', trimmed.slice(0, 60)),
      })
      .pipe(
        map((list) => list ?? []),
        catchError(() => of([])),
      );
  }
}
