import { Component, DestroyRef, ElementRef, computed, inject, signal, viewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { BadgeComponent, ButtonComponent, EmptyStateComponent } from '../../shared/components';
import { MessagingService } from '../../core/services/messaging.service';
import { RealtimeService } from '../../core/services/realtime.service';
import { toFailure } from '../../core/http/api-error';
import {
  Conversation,
  MESSAGE_MAX,
  Message,
  POLL_MS,
  bumpConversation,
  markRead,
  messageProblem,
  sentLabel,
  totalUnread,
  upsertMessage,
} from '../../core/models/messaging.model';

/**
 * Page 16. Every conversation on the left, the open thread on the right.
 *
 * The thread id lives in the query string rather than in a field, so a thread is a link
 * somebody can send, the back button steps between threads, and a refresh lands where it
 * left off. That is the same choice the search page makes about its filters.
 *
 * **Two channels, one thread.** The hub pushes a message the moment it is written; a poll
 * fetches the same message again a few seconds later if the hub is not connected. Both
 * feed `upsertMessage`, which is keyed on the id, so arriving twice draws one bubble. The
 * poll only runs while the hub is down - it is the fallback the brief requires, not a
 * second source of truth racing the first.
 */
@Component({
  selector: 'app-messages',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, BadgeComponent, ButtonComponent, EmptyStateComponent],
  templateUrl: './messages.component.html',
  styleUrls: ['./messages.component.scss'],
})
export class MessagesComponent {
  private readonly api = inject(MessagingService);
  private readonly realtime = inject(RealtimeService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  private readonly scroller = viewChild<ElementRef<HTMLElement>>('thread');

  readonly conversations = signal<Conversation[]>([]);
  readonly messages = signal<Message[]>([]);
  readonly activeId = signal<number | null>(null);

  readonly loading = signal(true);
  readonly threadLoading = signal(false);
  readonly sending = signal(false);
  readonly error = signal<string | null>(null);
  readonly sendError = signal<string | null>(null);

  readonly draft = signal('');

  readonly maxLength = MESSAGE_MAX;
  readonly sentLabel = sentLabel;
  readonly connected = this.realtime.connected;

  readonly unread = computed(() => totalUnread(this.conversations()));
  readonly active = computed(
    () => this.conversations().find((c) => c.id === this.activeId()) ?? null,
  );
  readonly remaining = computed(() => MESSAGE_MAX - this.draft().trim().length);

  constructor() {
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const raw = Number(params.get('c'));
      const id = Number.isInteger(raw) && raw > 0 ? raw : null;
      if (id === this.activeId()) return;

      this.activeId.set(id);
      this.messages.set([]);
      this.sendError.set(null);
      if (id !== null) this.openThread(id);
    });

    this.realtime.received$.pipe(takeUntilDestroyed()).subscribe((message) => this.arrive(message));

    // The fallback. It asks nothing of the server while the hub is holding the channel
    // open, which is most of the time, and keeps the product working when it is not.
    const timer = setInterval(() => {
      if (this.realtime.connected()) return;
      this.refresh();
    }, POLL_MS);
    this.destroyRef.onDestroy(() => clearInterval(timer));

    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.api.conversations().subscribe({
      next: (page) => {
        this.conversations.set(page.items ?? []);
        this.loading.set(false);
        this.pickFirstIfNoneChosen();
      },
      error: (err) => {
        this.error.set(toFailure(err).message);
        this.loading.set(false);
      },
    });
  }

  /**
   * Landing on `/messages` with no thread named opens the most recent one. An inbox whose
   * right-hand half is blank on arrival looks broken rather than empty.
   */
  private pickFirstIfNoneChosen(): void {
    if (this.activeId() !== null) return;
    const first = this.conversations()[0];
    if (first) this.select(first);
  }

  select(conversation: Conversation): void {
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { c: conversation.id },
      queryParamsHandling: 'merge',
    });
  }

  private openThread(id: number): void {
    this.threadLoading.set(true);

    this.api.messages(id).subscribe({
      next: (page) => {
        // The thread may have moved on while this was in flight - a push can land first.
        if (this.activeId() !== id) return;
        this.messages.set(page.items ?? []);
        this.threadLoading.set(false);
        this.scrollDown();
        this.clearUnread(id);
      },
      error: (err) => {
        if (this.activeId() !== id) return;
        this.error.set(toFailure(err).message);
        this.threadLoading.set(false);
      },
    });
  }

  /** Marking read twice is a 200 with nothing marked, so this never needs guarding. */
  private clearUnread(id: number): void {
    this.api.markRead(id).subscribe({
      next: () => this.conversations.update((list) => markRead(list, id)),
      error: () => undefined,
    });
  }

  /** A message off the hub, or off the poll. Both land here and both may be a repeat. */
  private arrive(message: Message): void {
    if (message.conversationId === this.activeId()) {
      this.messages.update((thread) => upsertMessage(thread, message));
      this.scrollDown();
      this.clearUnread(message.conversationId);
    }

    const known = this.conversations().some((c) => c.id === message.conversationId);
    if (!known) {
      // The first message on a thread this list has never seen. A message does not carry
      // enough to build a row out of, so the list is re-read rather than guessed at.
      this.refreshList();
      return;
    }
    this.conversations.update((list) => bumpConversation(list, message, this.activeId()));
  }

  private refresh(): void {
    this.refreshList();
    const id = this.activeId();
    if (id === null) return;

    this.api.messages(id).subscribe({
      next: (page) => {
        if (this.activeId() !== id) return;
        const before = this.messages().length;
        const after = (page.items ?? []).reduce(
          (thread, message) => upsertMessage(thread, message),
          this.messages(),
        );
        this.messages.set(after);
        if (after.length > before) {
          this.scrollDown();
          this.clearUnread(id);
        }
      },
      error: () => undefined,
    });
  }

  private refreshList(): void {
    this.api.conversations().subscribe({
      next: (page) => this.conversations.set(page.items ?? []),
      error: () => undefined,
    });
  }

  send(): void {
    const id = this.activeId();
    if (id === null) return;

    const body = this.draft().trim();
    const problem = messageProblem(body);
    if (problem) {
      this.sendError.set(problem);
      return;
    }

    this.sending.set(true);
    this.sendError.set(null);

    this.api.send(id, body).subscribe({
      next: (message) => {
        this.messages.update((thread) => upsertMessage(thread, message));
        this.draft.set('');
        this.sending.set(false);
        this.scrollDown();
        this.conversations.update((list) => bumpConversation(list, message, id));
      },
      error: (err) => {
        this.sendError.set(toFailure(err).message);
        this.sending.set(false);
      },
    });
  }

  /** Enter sends, Shift+Enter writes a new line - the shape everybody already expects. */
  onKey(event: KeyboardEvent): void {
    if (event.key !== 'Enter' || event.shiftKey) return;
    event.preventDefault();
    this.send();
  }

  private scrollDown(): void {
    // After the render that adds the bubble, not before it.
    setTimeout(() => {
      const element = this.scroller()?.nativeElement;
      if (element) element.scrollTop = element.scrollHeight;
    }, 0);
  }
}
