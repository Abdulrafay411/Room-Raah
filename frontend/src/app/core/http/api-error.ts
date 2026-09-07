import { HttpErrorResponse } from '@angular/common/http';
import { ApiErrorResponse, FieldErrorDto } from '../auth/auth.model';

/**
 * Turns whatever came back into something a screen can render.
 *
 * The API answers failures in one envelope and, on a 422, the `error` sentence is written
 * to be read by the person using the product - "You have already reviewed this listing.",
 * "Both beds were taken last week." Replacing those with a house style, or swallowing them
 * into "Something went wrong", throws away the only copy that actually explains the
 * refusal. So a 422 message is passed through verbatim, and this file exists mostly to
 * make doing that the easy path.
 */
export interface ApiFailure {
  /** What to put in front of the person. Never empty. */
  message: string;
  status: number;
  /** Present on a 400 only: one entry per field the server refused. */
  fieldErrors: FieldErrorDto[];
  /**
   * A 403 because the email has never been verified, rather than because of a role. The
   * brief is explicit that this routes to `/verify` and never shows a generic error - it
   * is the difference between "finish signing up" and "you are not allowed".
   */
  needsVerification: boolean;
  /** 429 only: how long until the next OTP may be asked for. */
  retryAfterSeconds?: number;
  /** 500 only: quote this when reporting a fault. */
  traceId?: string;
}

const GENERIC = 'Something went wrong. Please try again.';

/**
 * A 403 can mean "wrong role" or "your email is not verified", and the two need different
 * screens. The body distinguishes them when the server bothered to say so; otherwise the
 * caller decides, having already checked the token's `email_verified` claim.
 */
function looksUnverified(body: ApiErrorResponse | null): boolean {
  const text = body?.error?.toLowerCase() ?? '';
  return text.includes('verif');
}

export function toFailure(error: unknown): ApiFailure {
  if (!(error instanceof HttpErrorResponse)) {
    return { message: GENERIC, status: 0, fieldErrors: [], needsVerification: false };
  }

  const body = (error.error ?? null) as ApiErrorResponse | null;
  const server = typeof body?.error === 'string' && body.error.trim() ? body.error : null;

  // Status 0 is the browser refusing to tell us why - the network, or CORS. Saying so is
  // more use than a generic apology, because in this project it is almost always the API
  // box being off or the app being served from a port CORS does not admit.
  if (error.status === 0) {
    return {
      message: 'Cannot reach RoomRaah. Check your connection and try again.',
      status: 0,
      fieldErrors: [],
      needsVerification: false,
    };
  }

  const failure: ApiFailure = {
    message: server ?? GENERIC,
    status: error.status,
    fieldErrors: Array.isArray(body?.errors) ? body!.errors! : [],
    needsVerification: false,
  };

  switch (error.status) {
    case 400:
      failure.message = server ?? 'Please check the highlighted fields.';
      break;
    case 403:
      failure.needsVerification = looksUnverified(body);
      failure.message = server ?? 'You do not have permission to do that.';
      break;
    case 404:
      failure.message = server ?? 'That is no longer available.';
      break;
    case 429:
      failure.retryAfterSeconds = body?.retryAfterSeconds;
      failure.message =
        server ??
        (body?.retryAfterSeconds
          ? `Too many attempts. Try again in ${body.retryAfterSeconds} seconds.`
          : 'Too many attempts. Try again shortly.');
      break;
    case 500:
      failure.traceId = body?.traceId;
      failure.message = 'Sorry - something broke on our side. Please try again.';
      break;
    default:
      // 422 lands here, and that is the point: its sentence goes through untouched.
      break;
  }

  return failure;
}

/** Field errors keyed by control name. The server capitalises; Angular controls do not. */
export function fieldErrorMap(failure: ApiFailure): Record<string, string> {
  const map: Record<string, string> = {};
  for (const item of failure.fieldErrors) {
    const key = item.field.charAt(0).toLowerCase() + item.field.slice(1);
    map[key] = item.message;
  }
  return map;
}
