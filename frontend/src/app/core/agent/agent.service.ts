import { Injectable, effect, inject, signal, untracked } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { CompareStore } from '../services/compare.store';
import { GeolocationService } from '../services/geolocation.service';
import { buildSystemPrompt } from './agent-prompt';
import { AgentToolbox } from './agent-tools';
import { ChatEntry, LlmMessage, ToolContext, ToolOutcome } from './agent.model';
import { genderIn, looksRomanUrdu, readArgs, readYesNo, scrubContacts } from './agent.util';
import { OllamaClient } from './ollama.client';

/** A request that is still calling tools after this many rounds has lost its way. */
export const MAX_ROUNDS = 6;

/** How much of the conversation the model is sent again each turn, system prompt aside. */
export const HISTORY_LIMIT = 40;

export const OFFLINE_MESSAGE =
  'The assistant is offline - its model runs on this computer under Ollama, and Ollama is not answering. Start Ollama and try again.';

const STORAGE_KEY = 'roomraah.assistant';

/** Said, without asking the model, when the person turns an action down. */
export const NOT_DONE = 'Okay - I have not done that.';

const REPEATED = {
  repeated: true,
  note: 'You already made this exact call for this request and its result is above. It will not change. Answer the person now with what you have.',
};

/** The one question the assistant asks in its own words, and the buttons that answer it. */
export const GENDER_QUESTION = {
  urdu: 'Hostel kis ke liye chahiye - boys, girls ya family?',
  english: 'Who is the room for - boys, girls or a family?',
  options: ['Boys', 'Girls', 'Family', 'Any'],
};

const SKIPPED = { skipped: true, note: 'Not run, because the person declined an earlier step.' };

interface Answer {
  confirmed: boolean;
  /** What the person typed instead of answering, when they did. */
  said?: string;
}

interface Waiting {
  id: number;
  resolve: (answer: Answer) => void;
}

/**
 * The assistant's loop, and the conversation it keeps.
 *
 *   person → model → (tool → result → model)* → reply
 *
 * The model chooses which tools to call; this file only runs the loop, keeps the history,
 * and stops for the person whenever a tool would change something. It is a root singleton
 * so a conversation survives moving between pages, and it is copied to session storage so it
 * survives a reload of the tab as well.
 */
@Injectable({ providedIn: 'root' })
export class AgentService {
  private readonly llm = inject(OllamaClient);
  private readonly toolbox = inject(AgentToolbox);
  private readonly auth = inject(AuthService);
  private readonly geo = inject(GeolocationService);
  private readonly compare = inject(CompareStore);
  private readonly router = inject(Router);

  readonly entries = signal<ChatEntry[]>([]);
  readonly busy = signal(false);
  readonly online = signal<boolean | null>(null);

  private history: LlmMessage[] = [];
  private waiting: Waiting | null = null;
  private nextConfirmId = 1;
  private epoch = 0;
  /** Whose conversation this is. Undefined until known; null for a signed-out visitor. */
  private conversationFor: number | null | undefined = undefined;

  constructor() {
    this.restore();

    // One browser, two people: a conversation must not survive into somebody else's
    // session, where it would show them the last person's searches, visits and messages.
    effect(() => {
      const id = this.auth.currentUser()?.id ?? null;
      untracked(() => {
        if (this.conversationFor !== undefined && this.conversationFor !== id) this.clear();
        this.conversationFor = id;
        this.persist();
      });
    });
  }

  /** Called when the panel opens: loads the model early, and learns whether it is there. */
  wake(): void {
    if (this.online() === true) return;
    this.llm.warmUp().subscribe((ok) => this.online.set(ok));
  }

  async send(raw: string): Promise<void> {
    const text = raw.trim();
    if (!text) return;

    // While a confirm card waits, whatever is typed answers it - "haan" and "no" directly,
    // anything else as a no that the model then hears and responds to. The loop that asked
    // is still running, so starting a second one here would talk over it.
    if (this.waiting) {
      this.push({ kind: 'user', text });
      const answer = readYesNo(text);
      this.answer(answer === 'yes', answer === null ? text : undefined);
      return;
    }

    if (this.busy()) return;
    this.push({ kind: 'user', text });
    this.history.push({ role: 'user', content: text });
    await this.run();
  }

  /** The confirm card's two buttons. */
  confirm(id: number, confirmed: boolean): void {
    if (this.waiting?.id === id) this.answer(confirmed);
  }

  clear(): void {
    // A loop still running belongs to the conversation being thrown away; bumping the epoch
    // is how it learns to stop writing into the new one.
    this.epoch++;
    if (this.waiting) this.answer(false);
    this.entries.set([]);
    this.history = [];
    this.busy.set(false);
    this.persist();
  }

  private answer(confirmed: boolean, said?: string): void {
    const waiting = this.waiting;
    if (!waiting) return;
    this.waiting = null;
    this.entries.update((list) =>
      list.map((e) => (e.kind === 'confirm' && e.id === waiting.id ? { ...e, state: confirmed ? 'done' : 'declined' } : e)),
    );
    waiting.resolve({ confirmed, said });
  }

  private async run(): Promise<void> {
    const epoch = this.epoch;
    const current = () => epoch === this.epoch;
    this.busy.set(true);
    // Per request from the person: which exact calls have run, which hostels are already on
    // screen, and whether the model has used up its chances to go and look again.
    let seen = new Set<string>();
    let shown = new Set<number>();
    let mustAnswer = false;
    try {
      for (let round = 0; round < MAX_ROUNDS; round++) {
        // With no tools on offer the model can only answer, which is how a search that keeps
        // coming back the same is brought to an end instead of running out of rounds.
        const tools = mustAnswer || round === MAX_ROUNDS - 1 ? [] : this.toolbox.definitions();
        let reply: LlmMessage;
        try {
          reply = await firstValueFrom(this.llm.chat(this.messages(), tools));
        } catch {
          if (!current()) return;
          this.online.set(false);
          this.push({ kind: 'error', text: OFFLINE_MESSAGE });
          return;
        }
        if (!current()) return;
        this.online.set(true);

        const calls = reply.tool_calls ?? [];
        const isAdmin = this.auth.isAuthenticated() && this.auth.userRole() === 'Admin';
        const said = scrubContacts(reply.content ?? '', { allowEmail: isAdmin }).text.trim();
        this.history.push({ role: 'assistant', content: said, tool_calls: calls.length ? calls : undefined });

        if (calls.length === 0) {
          this.push({ kind: 'assistant', text: said || 'Sorry, I could not work that out. Could you say it another way?' });
          return;
        }
        // "Let me check..." between tool calls is the model thinking aloud; the panel's
        // working indicator already says that, so it stays in the history and off the screen.

        let declined: Answer | null = null;
        let asked = false;
        for (const call of calls) {
          const name = call.function.name;
          // Once the person has said no, the rest of this batch is not theirs any more either.
          if (declined) {
            this.history.push({ role: 'tool', tool_name: name, content: JSON.stringify(SKIPPED) });
            continue;
          }
          const args = readArgs(call.function.arguments);
          const key = callKey(name, args);
          if (seen.has(key)) {
            // A small model that dislikes an answer asks the same question again. It gets
            // the same answer, told so, and no second copy of the cards.
            this.history.push({ role: 'tool', tool_name: name, content: JSON.stringify(REPEATED) });
            mustAnswer = true;
            continue;
          }
          seen.add(key);

          const { outcome, answer } = await this.carryOut(name, args);
          if (!current()) return;
          if (outcome.ask === 'gender') {
            this.history.push({ role: 'tool', tool_name: name, content: JSON.stringify(outcome.result) });
            asked = true;
            break;
          }
          const fresh = (outcome.cards ?? []).filter((card) => !shown.has(card.id));
          fresh.forEach((card) => shown.add(card.id));
          if (fresh.length) this.push({ kind: 'cards', cards: fresh });
          if (outcome.links?.length && (fresh.length || !outcome.cards?.length)) {
            this.push({ kind: 'links', links: outcome.links });
          }
          this.history.push({ role: 'tool', tool_name: name, content: JSON.stringify(outcome.result) });
          if (answer && !answer.confirmed) declined = answer;
        }

        // Asked in fixed words rather than by the model, which would sometimes guess instead.
        if (asked) {
          for (const call of calls.slice(calls.findIndex((c) => c.function.name === 'search_hostels') + 1)) {
            this.history.push({ role: 'tool', tool_name: call.function.name, content: JSON.stringify(SKIPPED) });
          }
          const question = looksRomanUrdu(this.lastUserText()) ? GENDER_QUESTION.urdu : GENDER_QUESTION.english;
          this.history.push({ role: 'assistant', content: question });
          this.push({ kind: 'assistant', text: question });
          this.push({ kind: 'choices', options: GENDER_QUESTION.options });
          return;
        }

        // A "no" ends the action here rather than going back to the model: asked to follow
        // up on a cancelled save, a small model will cheerfully report that it saved. What
        // the person typed instead, if anything, then starts a fresh turn of its own.
        if (declined) {
          this.history.push({ role: 'assistant', content: NOT_DONE });
          this.push({ kind: 'assistant', text: NOT_DONE });
          if (!declined.said) return;
          this.history.push({ role: 'user', content: declined.said });
          seen = new Set();
          shown = new Set();
          mustAnswer = false;
        }
      }
      this.push({ kind: 'assistant', text: 'That took more steps than it should have. Could you ask again, a little more simply?' });
    } finally {
      if (current()) {
        this.busy.set(false);
        this.persist();
      }
    }
  }

  private async carryOut(
    name: string,
    args: Record<string, unknown>,
  ): Promise<{ outcome: ToolOutcome; answer?: Answer }> {
    const prepared = await this.toolbox.prepare(name, args, this.context());
    if ('outcome' in prepared) return { outcome: prepared.outcome };

    const id = this.nextConfirmId++;
    this.push({ kind: 'confirm', id, summary: prepared.pending.summary, state: 'waiting' });
    this.busy.set(false);
    const answer = await new Promise<Answer>((resolve) => (this.waiting = { id, resolve }));
    this.busy.set(true);

    if (!answer.confirmed) {
      return { outcome: { result: { cancelled: true, done: false, note: 'The person did not confirm. Nothing was changed.' } }, answer };
    }
    try {
      return { outcome: await prepared.pending.run(), answer };
    } catch {
      return { outcome: { result: { error: 'The website could not complete that. Nothing was changed.' } }, answer };
    }
  }

  /**
   * What the conversation says that one tool call cannot: whether "who is it for" was asked
   * already, and what the person last said about it in their own words.
   */
  private context(): ToolContext {
    const genderAsked = this.history.some((m) => m.role === 'tool' && m.content.includes('"askFirst":"gender"'));
    const said = this.history.filter((m) => m.role === 'user').map((m) => m.content);
    let genderHint: ToolContext['genderHint'];
    // "Any" only counts as the answer to the question, i.e. in the latest message; a named
    // gender counts from wherever the person last said one.
    if (said.length && genderIn(said[said.length - 1]) === 'Any') genderHint = 'Any';
    for (let i = said.length - 1; i >= 0 && !genderHint; i--) {
      const found = genderIn(said[i]);
      if (found && found !== 'Any') genderHint = found;
    }
    return { genderAsked, genderHint };
  }

  private lastUserText(): string {
    for (let i = this.history.length - 1; i >= 0; i--) {
      if (this.history[i].role === 'user') return this.history[i].content;
    }
    return '';
  }

  private messages(): LlmMessage[] {
    const user = this.auth.currentUser();
    const system = buildSystemPrompt({
      now: new Date(),
      signedIn: this.auth.isAuthenticated(),
      name: user?.fullName ?? null,
      role: this.auth.userRole(),
      emailVerified: this.auth.isEmailVerified(),
      hasPosition: this.geo.hasPosition(),
      currentUrl: this.router.url,
      compareList: this.compare.ids(),
    });
    return [{ role: 'system', content: system }, ...trimHistory(this.history, HISTORY_LIMIT)];
  }

  private push(entry: ChatEntry): void {
    this.entries.update((list) => [...list, entry]);
    this.persist();
  }

  private persist(): void {
    try {
      // A confirm card cannot outlive the promise behind it, so a reload shows it as declined.
      const entries = this.entries().map((e) => (e.kind === 'confirm' && e.state === 'waiting' ? { ...e, state: 'declined' as const } : e));
      sessionStorage.setItem(
        STORAGE_KEY,
        JSON.stringify({ entries, history: this.history, userId: this.conversationFor ?? null }),
      );
    } catch {
      // Storage switched off: the conversation still works, it just does not survive a reload.
    }
  }

  private restore(): void {
    try {
      const raw = sessionStorage.getItem(STORAGE_KEY);
      if (!raw) return;
      const saved = JSON.parse(raw) as { entries?: ChatEntry[]; history?: LlmMessage[]; userId?: number | null };
      this.conversationFor = saved.userId ?? null;
      if (Array.isArray(saved.entries)) this.entries.set(saved.entries);
      if (Array.isArray(saved.history)) this.history = saved.history;
      const maxId = Math.max(0, ...this.entries().map((e) => (e.kind === 'confirm' ? e.id : 0)));
      this.nextConfirmId = maxId + 1;
    } catch {
      // A corrupt copy is a fresh conversation, not an error.
    }
  }
}

/** Two calls are the same call when the tool and every argument match, in any order. */
export function callKey(name: string, args: Record<string, unknown>): string {
  const sorted = Object.keys(args)
    .sort()
    .map((k) => [k, args[k]]);
  return name + JSON.stringify(sorted);
}

/**
 * Keeps the latest messages without cutting a tool answer off from the call it answers:
 * the kept window always starts at a person's message.
 */
export function trimHistory(history: LlmMessage[], limit: number): LlmMessage[] {
  if (history.length <= limit) return history;
  let start = history.length - limit;
  while (start < history.length && history[start].role !== 'user') start++;
  return history.slice(start);
}
