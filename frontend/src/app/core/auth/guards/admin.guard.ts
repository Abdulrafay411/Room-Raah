import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../auth.service';
import { SessionDecision, toHome, withSession } from './session';

export const adminGuard: CanActivateFn = (route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return withSession(auth, router, state, (): SessionDecision =>
    auth.userRole() === 'Admin' ? true : toHome(router),
  );
};
