import { Injectable, effect, inject, signal } from '@angular/core';
import { Subject } from 'rxjs';
// Type-only, so nothing from the package is in the compiled output. The runtime import is
// the dynamic one in `open()` below, and that is the whole point of this file's shape.
import type { HubConnection } from '@microsoft/signalr';
import { environment } from '../../../environments/environment';
import { AuthService } from '../auth/auth.service';
import { Message } from '../models/messaging.model';

/**
 * The live half of messaging: one hub connection, held open for as long as somebody is
 * signed in.
 *
 * **Why it is app-wide and not on the messages page.** The server emails a "you have a new
 * message" nudge to anybody it cannot push to. A user sitting on the search page with no
 * connection therefore gets an email instead of a badge - so the connection follows the
 * session, not the route.
 *
 * **Why the import is dynamic.** This service is reached from the app shell, and a static
 * import would put the SignalR client in the initial bundle for every visitor, including
 * the ones who never sign in. `import()` puts it in its own chunk that is fetched the
 * first time somebody signs in. The type import above is erased at compile time and costs
 * nothing.
 *
 * **Why nothing here throws.** Messaging must work without WebSockets - that is the
 * brief's requirement, not a nicety - so every failure path ends in `connected` being
 * false and the page falling back to polling. A hub that cannot connect is a slower
 * product, never a broken one.
 */
@Injectable({ providedIn: 'root' })
export class RealtimeService {
  private readonly auth = inject(AuthService);

  private connection: HubConnection | null = null;

  /**
   * Which user the current connection belongs to. Signing out and in as somebody else has
   * to build a new connection; a token merely rotating by silent refresh does not, because
   * `accessTokenFactory` is read again on every reconnect and picks the new one up itself.
   */
  private openFor: number | null = null;

  /** Whether the push channel is live. The messages page polls while this is false. */
  readonly connected = signal(false);

  private readonly received = new Subject<Message>();
  readonly received$ = this.received.asObservable();

  constructor() {
    effect(() => {
      const user = this.auth.isAuthenticated() ? (this.auth.currentUser()?.id ?? null) : null;
      if (user === this.openFor) return;
      this.openFor = user;
      void this.rebuild(user);
    });
  }

  private async rebuild(user: number | null): Promise<void> {
    await this.close();
    if (user === null) return;
    await this.open();
  }

  private async open(): Promise<void> {
    // No hub in a non-browser run. The unit tests instantiate the app shell, and a
    // connection attempt there is a hang, not a test.
    if (typeof window === 'undefined') return;

    try {
      const signalr = await import('@microsoft/signalr');

      const connection = new signalr.HubConnectionBuilder()
        .withUrl(environment.hubUrl, {
          // A WebSocket handshake cannot carry an Authorization header, so the token goes
          // in the query string and the API reads `access_token` for this path only.
          accessTokenFactory: () => this.auth.accessToken ?? '',
        })
        .withAutomaticReconnect()
        .build();

      connection.on('MessageReceived', (message: Message) => this.received.next(message));
      connection.onreconnected(() => this.connected.set(true));
      connection.onreconnecting(() => this.connected.set(false));
      connection.onclose(() => this.connected.set(false));

      await connection.start();

      // Signing out during the handshake: the session this connection was built for is
      // gone, so drop it rather than leaving a live socket for a user who has left.
      if (this.openFor === null) {
        await connection.stop().catch(() => undefined);
        return;
      }

      this.connection = connection;
      this.connected.set(true);
    } catch {
      this.connected.set(false);
    }
  }

  private async close(): Promise<void> {
    const connection = this.connection;
    this.connection = null;
    this.connected.set(false);
    if (!connection) return;
    await connection.stop().catch(() => undefined);
  }
}
