import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../auth.service';
import { SessionDecision, withSession } from './session';

export const verifiedGuard: CanActivateFn = (route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return withSession(auth, router, state, (): SessionDecision => {
    if (auth.isEmailVerified()) {
      return true;
    }

    // Rule 3 of AGENTS.md §3: an unverified account is routed to /verify, never shown a
    // generic error.
    return router.createUrlTree(['/verify']);
  });
};
