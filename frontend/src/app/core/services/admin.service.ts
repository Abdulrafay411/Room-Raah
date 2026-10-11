import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AdminProperty,
  AdminPropertyPage,
  AdminUserDetail,
  AdminUserPage,
  AdminUserSummary,
  InspectionPage,
  InspectionRecord,
  RequestedInspectionPage,
  ReportPage,
  ReportRow,
  ReportStatus,
  ReviewQueuePage,
  ReviewQueueRow,
  ReviewState,
  RevisionDetail,
  RevisionKind,
  RevisionPage,
  VerificationDraft,
} from '../models/admin.model';
import { PropertyStatus } from '../models/owner.model';

/** Rows per page on every admin queue. Twenty fits a laptop screen beside the detail. */
export const ADMIN_PAGE_SIZE = 20;

@Injectable({ providedIn: 'root' })
export class AdminService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/admin`;

  /** `q` is rule 135: title, city or owner's display name. */
  properties(status?: PropertyStatus, page = 1, q?: string): Observable<AdminPropertyPage> {
    let params = new HttpParams().set('page', page).set('pageSize', ADMIN_PAGE_SIZE);
    if (status) params = params.set('status', status);
    if (q?.trim()) params = params.set('q', q.trim());
    return this.http.get<AdminPropertyPage>(`${this.base}/properties`, { params });
  }

  property(id: number): Observable<AdminProperty> {
    return this.http.get<AdminProperty>(`${this.base}/properties/${id}`);
  }

  verify(id: number, draft: VerificationDraft): Observable<AdminProperty['verificationChecks']> {
    return this.http.post<AdminProperty['verificationChecks']>(`${this.base}/properties/${id}/verify`, draft);
  }

  decideProperty(
    id: number,
    action: 'approve' | 'reject' | 'request-changes' | 'unpublish',
    adminNote: string,
  ): Observable<AdminProperty> {
    return this.http.post<AdminProperty>(`${this.base}/properties/${id}/${action}`, {
      adminNote: adminNote.trim() || null,
    });
  }

  revisions(kind: RevisionKind, page = 1): Observable<RevisionPage> {
    const params = new HttpParams().set('kind', kind).set('page', page).set('pageSize', ADMIN_PAGE_SIZE);
    return this.http.get<RevisionPage>(`${this.base}/revisions`, { params });
  }

  revision(id: number): Observable<RevisionDetail> {
    return this.http.get<RevisionDetail>(`${this.base}/revisions/${id}`);
  }

  decideRevision(id: number, action: 'approve' | 'reject', adminNote: string): Observable<RevisionDetail> {
    return this.http.post<RevisionDetail>(`${this.base}/revisions/${id}/${action}`, {
      adminNote: adminNote.trim() || null,
    });
  }

  dueInspections(page = 1): Observable<InspectionPage> {
    const params = new HttpParams().set('page', page).set('pageSize', ADMIN_PAGE_SIZE);
    return this.http.get<InspectionPage>(`${this.base}/inspections/due`, { params });
  }

  /** Rule 136: owners waiting for an inspection, oldest request first. */
  requestedInspections(page = 1): Observable<RequestedInspectionPage> {
    const params = new HttpParams().set('page', page).set('pageSize', ADMIN_PAGE_SIZE);
    return this.http.get<RequestedInspectionPage>(`${this.base}/inspections/requested`, { params });
  }

  recordInspection(body: {
    propertyId: number;
    inspectedAt: string;
    result: 'Passed' | 'Failed';
    notes: string | null;
    feeAmount: number | null;
    feeCollectedAt: string | null;
  }): Observable<InspectionRecord> {
    return this.http.post<InspectionRecord>(`${this.base}/inspections`, body);
  }

  changeBadge(id: number, action: 'grant' | 'remove', reason: string): Observable<AdminProperty> {
    return this.http.post<AdminProperty>(`${this.base}/properties/${id}/badge/${action}`, { reason });
  }

  reviews(state?: ReviewState, page = 1): Observable<ReviewQueuePage> {
    let params = new HttpParams().set('page', page).set('pageSize', ADMIN_PAGE_SIZE);
    if (state) params = params.set('state', state);
    return this.http.get<ReviewQueuePage>(`${this.base}/reviews`, { params });
  }

  moderateReview(id: number, state: Exclude<ReviewState, 'Pending'>): Observable<ReviewQueueRow> {
    return this.http.patch<ReviewQueueRow>(`${this.base}/reviews/${id}`, { state });
  }

  reports(status?: ReportStatus, page = 1): Observable<ReportPage> {
    let params = new HttpParams().set('page', page).set('pageSize', ADMIN_PAGE_SIZE);
    if (status) params = params.set('status', status);
    return this.http.get<ReportPage>(`${this.base}/reports`, { params });
  }

  resolveReport(id: number, status: Exclude<ReportStatus, 'Open'>, adminNote: string): Observable<ReportRow> {
    return this.http.patch<ReportRow>(`${this.base}/reports/${id}`, {
      status,
      adminNote: adminNote.trim() || null,
    });
  }

  users(filters: { role?: string; isActive?: string; q?: string }, page = 1): Observable<AdminUserPage> {
    let params = new HttpParams().set('page', page).set('pageSize', ADMIN_PAGE_SIZE);
    if (filters.role) params = params.set('role', filters.role);
    if (filters.isActive) params = params.set('isActive', filters.isActive);
    if (filters.q?.trim()) params = params.set('q', filters.q.trim());
    return this.http.get<AdminUserPage>(`${this.base}/users`, { params });
  }

  user(id: number): Observable<AdminUserDetail> {
    return this.http.get<AdminUserDetail>(`${this.base}/users/${id}`);
  }

  setUserActive(id: number, active: boolean, reason: string): Observable<AdminUserSummary> {
    const action = active ? 'reactivate' : 'deactivate';
    return this.http.patch<AdminUserSummary>(`${this.base}/users/${id}/${action}`, { reason });
  }
}
