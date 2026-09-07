import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../auth.service';
import { SessionDecision, toHome, withSession } from './session';

export const ownerGuard: CanActivateFn = (route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return withSession(auth, router, state, (): SessionDecision =>
    auth.userRole() === 'Owner' ? true : toHome(router),
  );
};
