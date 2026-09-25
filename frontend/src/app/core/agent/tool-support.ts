/**
 * What every toolbox needs and none of them owns: running a write, turning a refusal into
 * words the model can pass on, and writing a date the way a person checks a booking.
 */
import { Router } from '@angular/router';
import { Observable, firstValueFrom } from 'rxjs';
import { toFailure } from '../http/api-error';
import { AgentLink, PendingAction, ToolOutcome } from './agent.model';

export type Args = Record<string, unknown>;

/** What running a tool call can come to: an answer now, or a question for the person first. */
export type Prepared = { outcome: ToolOutcome } | { pending: PendingAction };

export const required = (what: string): Prepared => ({ outcome: { result: { error: `${what} is required.` } } });

/** Runs a write and turns a refusal into words the model can pass on. */
export async function runWrite(request: Observable<unknown>, success: ToolOutcome, router: Router): Promise<ToolOutcome> {
  try {
    await firstValueFrom(request);
    return success;
  } catch (err) {
    return toolFailure(err, router);
  }
}

/**
 * A refused call, as the model should hear it. A 422's sentence is the API's own and goes
 * through untouched - it names the exact rule that was hit. Rule 3: a 403 for an unverified
 * email is `/verify`, never a generic error.
 */
export function toolFailure(err: unknown, router: Router): ToolOutcome {
  const failure = toFailure(err);
  if (failure.status === 403 && failure.needsVerification) {
    router.navigate(['/verify']);
    return {
      result: { error: 'Their email is not verified, so the site refused. They have been taken to the verify page.' },
      links: [{ label: 'Verify your email', commands: ['/verify'] }],
    };
  }
  const links: AgentLink[] | undefined =
    failure.status === 401 ? [{ label: 'Sign in', commands: ['/login'], queryParams: { returnUrl: router.url } }] : undefined;
  return { result: { error: failure.message, status: failure.status }, links };
}

/** "Fri, 26 Sep, 5:00 pm" - the form a person checks a booking against. */
export function when(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return date.toLocaleString('en-GB', {
    weekday: 'short',
    day: 'numeric',
    month: 'short',
    hour: 'numeric',
    minute: '2-digit',
  });
}
