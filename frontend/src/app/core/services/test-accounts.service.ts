import { HttpBackend, HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, shareReplay, switchMap, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthSessionDto } from '../auth/auth.model';
import { AccountRole, AdminUserPage, AdminUserSummary } from '../models/admin.model';

/** The password every seeded and demo account is created with. */
export const TEST_PASSWORD = 'Password1';
const TEST_ADMIN = 'admin@roomraah.local';

/** Seed and demo accounts live on this domain; anything else was registered by a person. */
export function knownPassword(email: string): string | null {
  return email.toLowerCase().endsWith('@roomraah.local') ? TEST_PASSWORD : null;
}

/**
 * TESTING ONLY - backs the login page's quick sign-in. There is no public way to list
 * accounts, so this signs in as the seeded admin and reads the admin user list.
 *
 * It runs on a bare HttpClient built from HttpBackend: no interceptor, so the visitor's
 * own token is never swapped in or refreshed, and no session is written - looking up
 * accounts does not sign anybody in.
 */
@Injectable({ providedIn: 'root' })
export class TestAccountsService {
  private readonly http = new HttpClient(inject(HttpBackend));
  private readonly base = environment.apiBaseUrl;
  private adminToken: Observable<string> | null = null;

  /** The first 50 accounts of one role - the admin list's largest page. */
  accounts(role: AccountRole): Observable<AdminUserSummary[]> {
    return this.token().pipe(
      switchMap((token) =>
        this.http.get<AdminUserPage>(`${this.base}/admin/users`, {
          params: new HttpParams().set('role', role).set('pageSize', 50),
          headers: { Authorization: `Bearer ${token}` },
        }),
      ),
      map((page) => page.items),
      catchError((error) => {
        // A refused or expired token is fetched afresh on the next pick.
        this.adminToken = null;
        return throwError(() => error);
      }),
    );
  }

  private token(): Observable<string> {
    this.adminToken ??= this.http
      .post<AuthSessionDto>(`${this.base}/auth/login`, { email: TEST_ADMIN, password: TEST_PASSWORD })
      .pipe(
        map((session) => session.accessToken),
        shareReplay(1),
      );
    return this.adminToken;
  }
}
