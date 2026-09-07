import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Page } from '../models/catalog.model';
import {
  Conversation,
  ConversationCreated,
  Message,
  ReadReceipt,
} from '../models/messaging.model';

/**
 * The REST half of messaging. The hub is the other half and lives in `RealtimeService`;
 * everything here works with the hub switched off, which is the brief's requirement, not
 * a fallback bolted on afterwards.
 */
@Injectable({ providedIn: 'root' })
export class MessagingService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/conversations`;

  /**
   * Opens the thread for a listing, or hands back the one that already exists - a 200
   * rather than a 201, and not an error. So there is never any reason to look first.
   */
  open(propertyId: number): Observable<ConversationCreated> {
    return this.http.post<ConversationCreated>(this.base, { propertyId });
  }

  /** Most recent message first. */
  conversations(page = 1, pageSize = 50): Observable<Page<Conversation>> {
    return this.http.get<Page<Conversation>>(this.base, {
      params: new HttpParams().set('page', page).set('pageSize', pageSize),
    });
  }

  /** Oldest first, which is the order a thread is read in. */
  messages(conversationId: number, page = 1, pageSize = 50): Observable<Page<Message>> {
    return this.http.get<Page<Message>>(`${this.base}/${conversationId}/messages`, {
      params: new HttpParams().set('page', page).set('pageSize', pageSize),
    });
  }

  send(conversationId: number, body: string): Observable<Message> {
    return this.http.post<Message>(`${this.base}/${conversationId}/messages`, { body });
  }

  /**
   * Safe to call whenever the thread is on screen. A second call marks nothing and still
   * answers 200 with a `markedCount` of zero.
   */
  markRead(conversationId: number): Observable<ReadReceipt> {
    return this.http.patch<ReadReceipt>(`${this.base}/${conversationId}/read`, {});
  }
}
