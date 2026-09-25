import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, of } from 'rxjs';
import { catchError, map, timeout } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { LlmMessage, ToolDefinition } from './agent.model';

/**
 * The first answer after the model has been idle includes loading 6.6 GB into the GPU,
 * which on this laptop is ten to fifteen seconds. Everything after that is one to three.
 */
export const LLM_TIMEOUT_MS = 90_000;

/**
 * Sent with every call, the warm-up included: a different context size makes Ollama unload
 * and reload the model, which would turn the warm-up into a second cold start.
 */
const OPTIONS = { temperature: 0.2, num_ctx: 16384 };

/**
 * The only file that talks to the model.
 *
 * Ollama answers CORS for `localhost` origins out of the box, so the browser calls it
 * directly. The URL is not under `apiBaseUrl`, which keeps the auth interceptor from ever
 * attaching a RoomRaah token to a request that leaves for somewhere else.
 */
@Injectable({ providedIn: 'root' })
export class OllamaClient {
  private readonly http = inject(HttpClient);
  private readonly base = environment.ollamaUrl;
  private readonly model = environment.agentModel;

  chat(messages: LlmMessage[], tools: ToolDefinition[]): Observable<LlmMessage> {
    return this.http
      .post<{ message: LlmMessage }>(`${this.base}/api/chat`, {
        model: this.model,
        messages,
        tools,
        stream: false,
        // Qwen reasons out loud first when allowed to. Tool choice does not need it, and it
        // triples the wait for every answer.
        think: false,
        keep_alive: '30m',
        options: OPTIONS,
      })
      .pipe(
        timeout(LLM_TIMEOUT_MS),
        map((body) => body.message),
      );
  }

  /**
   * Loads the model while the person is still typing, so their first answer is not the
   * slow one. Answers whether Ollama is there at all - false is how the panel knows to say
   * the assistant is offline instead of spinning.
   */
  warmUp(): Observable<boolean> {
    return this.http
      .post(`${this.base}/api/generate`, { model: this.model, keep_alive: '30m', options: OPTIONS })
      .pipe(
        timeout(LLM_TIMEOUT_MS),
        map(() => true),
        catchError(() => of(false)),
      );
  }
}
