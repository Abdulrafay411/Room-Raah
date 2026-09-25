import { Component, ElementRef, computed, effect, inject, signal, viewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AgentService } from '../../core/agent/agent.service';
import { ChatEntry } from '../../core/agent/agent.model';
import { ReplyLine, rupees, toReplyLines } from '../../core/agent/agent.util';
import { CompareStore } from '../../core/services/compare.store';
import { AuthService } from '../../core/auth/auth.service';

/** Where a first-time visitor might start. Each is a real request the tools can answer. */
export const STARTERS = [
  'Boys hostel in Lahore under 25k with WiFi',
  'Mere nazdeek koi hostel dikhao',
  'How do visit requests work?',
];

/** An admin comes to work the queues. */
export const ADMIN_STARTERS = [
  'Kaunsi listings review ke liye pending hain?',
  'Show pending content revisions',
  'Open reports dikhao',
];

/** An owner comes to run listings, not to find a room. */
export const OWNER_STARTERS = [
  'Meri listings dikhao',
  'Koi naye visit requests aaye hain?',
  'Update the free beds on my listing',
];

/**
 * The floating assistant: a button in the corner, and the conversation behind it.
 *
 * Everything it knows comes from `AgentService`; this only draws. Replies are drawn from
 * parsed runs of text rather than bound as HTML, so nothing the model writes can become
 * markup on the page.
 */
@Component({
  selector: 'app-assistant-widget',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './assistant-widget.component.html',
  styleUrls: ['./assistant-widget.component.scss'],
})
export class AssistantWidgetComponent {
  readonly agent = inject(AgentService);
  readonly compare = inject(CompareStore);

  readonly open = signal(false);
  readonly draft = signal('');
  private readonly auth = inject(AuthService);

  readonly role = computed(() => (this.auth.isAuthenticated() ? this.auth.userRole() : null));
  readonly starters = computed(() =>
    this.role() === 'Owner' ? OWNER_STARTERS : this.role() === 'Admin' ? ADMIN_STARTERS : STARTERS,
  );
  readonly rupees = rupees;

  /** A confirm card is waiting, so typing answers it even while the loop is paused. */
  readonly waiting = computed(() =>
    this.agent.entries().some((e) => e.kind === 'confirm' && e.state === 'waiting'),
  );

  /** Each reply parsed once, not on every change detection pass. */
  readonly view = computed(() =>
    this.agent.entries().map((entry) => ({
      entry,
      lines: entry.kind === 'assistant' ? toReplyLines(entry.text) : ([] as ReplyLine[]),
    })),
  );

  private readonly log = viewChild<ElementRef<HTMLElement>>('log');
  private readonly input = viewChild<ElementRef<HTMLTextAreaElement>>('input');

  constructor() {
    // Keep the latest message in view as the conversation grows.
    effect(() => {
      this.agent.entries();
      this.agent.busy();
      const log = this.log()?.nativeElement;
      if (log) queueMicrotask(() => (log.scrollTop = log.scrollHeight));
    });
  }

  toggle(): void {
    this.open.update((v) => !v);
    if (this.open()) {
      this.agent.wake();
      queueMicrotask(() => this.input()?.nativeElement.focus());
    }
  }

  submit(text = this.draft()): void {
    if (!text.trim() || (this.agent.busy() && !this.waiting())) return;
    this.draft.set('');
    void this.agent.send(text);
  }

  /** Enter sends; Shift+Enter is a new line, as in every chat box. */
  onKey(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.submit();
    }
  }

  trackEntry(index: number, item: { entry: ChatEntry }): string {
    return `${index}-${item.entry.kind}`;
  }
}
