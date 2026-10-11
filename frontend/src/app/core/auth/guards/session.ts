import { Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { Observable, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { AuthService } from '../auth.service';

/**
 * What a guard here can answer with: allow, or send somewhere else. Deliberately narrower
 * than the router's own `GuardResult`, which also admits a `RedirectCommand` — nothing in
 * this application needs one, and the narrower type keeps `withSession` honest.
 */
export type SessionDecision = boolean | UrlTree;

/**
 * Every guard has to answer the same question before it can answer its own: is there a
 * session at all? An expired access token is not the same as a signed-out person — the
 * refresh token is usually still good, and the whole point of silent refresh is that
 * coming back tomorrow does not feel like being logged out.
 *
 * This lives in one place because it did not, once. authGuard and verifiedGuard learned
 * to refresh; ownerGuard and adminGuard did not, and an owner returning with an expired
 * access token was quietly redirected away from their own dashboard. Guards that share a
 * precondition should share the code for it.
 */
export function withSession(
  auth: AuthService,
  router: Router,
  state: RouterStateSnapshot,
  decide: () => SessionDecision,
): SessionDecision | Observable<SessionDecision> {
  if (auth.isAuthenticated()) {
    return decide();
  }

  if (auth.refreshToken) {
    return auth.refreshSession().pipe(
      map(() => decide()),
      catchError(() => of(toLogin(router, state))),
    );
  }

  return toLogin(router, state);
}

/** Signed out, or the refresh token is spent: ask them to sign in, and come back here. */
export function toLogin(router: Router, state: RouterStateSnapshot): UrlTree {
  return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
}

/** Why an unverified account was sent to `/verify` - said there in one sentence. */
export type VerifyReason = 'save' | 'message' | 'visit' | 'review' | 'report' | 'page' | 'action';

/**
 * Query parameters for `/verify`: where to come back to once the code is accepted, and what
 * they were trying to do. A return to `/` or to verify itself carries nothing, so it is left
 * out. Every route into verify builds its parameters here, so they all read the same way.
 */
export function verifyParams(context: {
  returnUrl?: string | null;
  reason?: VerifyReason;
  email?: string;
}): Record<string, string> {
  const params: Record<string, string> = {};
  if (context.email) params['email'] = context.email;
  const back = context.returnUrl;
  if (back && back !== '/' && !back.startsWith('/verify')) params['returnUrl'] = back;
  if (context.reason) params['reason'] = context.reason;
  return params;
}

/**
 * Signed in, wrong role. Home rather than the sign-in screen: signing in again would not
 * help, and it would look like the session had broken. `/` is the landing page, which is the
 * one page everybody is allowed to see.
 */
export function toHome(router: Router): UrlTree {
  return router.createUrlTree(['/']);
}
