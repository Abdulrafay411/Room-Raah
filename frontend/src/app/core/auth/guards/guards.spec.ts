import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { MaybeAsync, GuardResult as RouterGuardResult } from '@angular/router';
import { isObservable, of, throwError } from 'rxjs';
import { adminGuard, authGuard, ownerGuard, verifiedGuard } from './index';
import { SessionDecision } from './session';
import { AuthService } from '../auth.service';
import { UserRole } from '../auth.model';

describe('Route Guards', () => {
  let isAuthSignal = signal<boolean>(false);
  let isVerifiedSignal = signal<boolean>(false);
  let userRoleSignal = signal<UserRole | null>(null);
  let router: Router;
  let mockAuthService: {
    isAuthenticated: typeof isAuthSignal;
    isEmailVerified: typeof isVerifiedSignal;
    userRole: typeof userRoleSignal;
    refreshToken: string | null;
    refreshSession: ReturnType<typeof vi.fn>;
  };

  const mockRoute = {} as ActivatedRouteSnapshot;
  const mockState = { url: '/protected-page' } as RouterStateSnapshot;

  /**
   * Settles a guard's answer, whether it answered at once or after a refresh.
   *
   * Written this way because the obvious form is a trap: `result$.subscribe(v =>
   * expect(v).toBe(true))` passes when the observable never emits at all, which is one of
   * the failures these tests exist to catch. Here a guard that never answers fails.
   */
  function settle(result: MaybeAsync<RouterGuardResult>): SessionDecision {
    if (result instanceof Promise) {
      throw new Error('these guards answer synchronously or with an observable, never a promise');
    }

    if (!isObservable(result)) {
      return result as SessionDecision;
    }

    let emitted: SessionDecision | undefined;
    let emissions = 0;

    result.subscribe((value) => {
      emitted = value as SessionDecision;
      emissions++;
    });

    expect(emissions).toBe(1);
    return emitted!;
  }

  const loginUrlTree = {
    commands: ['/login'],
    extras: { queryParams: { returnUrl: '/protected-page' } },
  };
  const homeUrlTree = { commands: ['/'], extras: undefined };

  beforeEach(() => {
    isAuthSignal = signal<boolean>(false);
    isVerifiedSignal = signal<boolean>(false);
    userRoleSignal = signal<UserRole | null>(null);

    mockAuthService = {
      isAuthenticated: isAuthSignal,
      isEmailVerified: isVerifiedSignal,
      userRole: userRoleSignal,
      refreshToken: null,
      refreshSession: vi.fn(),
    };

    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: mockAuthService },
        {
          provide: Router,
          useValue: {
            createUrlTree: vi.fn((commands, extras) => ({ commands, extras }) as unknown as UrlTree),
          },
        },
      ],
    });

    router = TestBed.inject(Router);
  });

  describe('authGuard', () => {
    it('should allow navigation when authenticated', () => {
      isAuthSignal.set(true);

      expect(settle(TestBed.runInInjectionContext(() => authGuard(mockRoute, mockState)))).toBe(
        true,
      );
    });

    it('should redirect to /login with returnUrl when unauthenticated and no refreshToken', () => {
      isAuthSignal.set(false);
      mockAuthService.refreshToken = null;

      expect(settle(TestBed.runInInjectionContext(() => authGuard(mockRoute, mockState)))).toEqual(
        loginUrlTree,
      );
    });

    it('should silently refresh and allow navigation when access token expired but refreshToken succeeds', () => {
      isAuthSignal.set(false);
      mockAuthService.refreshToken = 'valid-refresh-token';
      mockAuthService.refreshSession.mockReturnValue(of({ accessToken: 'new-token' }));

      expect(settle(TestBed.runInInjectionContext(() => authGuard(mockRoute, mockState)))).toBe(
        true,
      );
      expect(mockAuthService.refreshSession).toHaveBeenCalled();
    });

    it('should redirect to /login when refreshToken fails during silent refresh', () => {
      isAuthSignal.set(false);
      mockAuthService.refreshToken = 'bad-refresh-token';
      mockAuthService.refreshSession.mockReturnValue(throwError(() => new Error('Refresh failed')));

      expect(settle(TestBed.runInInjectionContext(() => authGuard(mockRoute, mockState)))).toEqual(
        loginUrlTree,
      );
    });
  });

  describe('verifiedGuard', () => {
    it('should allow navigation when authenticated and email is verified', () => {
      isAuthSignal.set(true);
      isVerifiedSignal.set(true);

      expect(settle(TestBed.runInInjectionContext(() => verifiedGuard(mockRoute, mockState)))).toBe(
        true,
      );
    });

    it('should redirect to /verify when authenticated but email is not verified', () => {
      isAuthSignal.set(true);
      isVerifiedSignal.set(false);

      settle(TestBed.runInInjectionContext(() => verifiedGuard(mockRoute, mockState)));
      expect(router.createUrlTree).toHaveBeenCalledWith(['/verify']);
    });

    it('should redirect to /login when unauthenticated and no refreshToken', () => {
      isAuthSignal.set(false);
      mockAuthService.refreshToken = null;

      expect(
        settle(TestBed.runInInjectionContext(() => verifiedGuard(mockRoute, mockState))),
      ).toEqual(loginUrlTree);
    });

    it('should silently refresh and allow navigation if verified after refresh', () => {
      isAuthSignal.set(false);
      isVerifiedSignal.set(true);
      mockAuthService.refreshToken = 'valid-refresh-token';
      mockAuthService.refreshSession.mockReturnValue(of({ accessToken: 'new-token' }));

      expect(settle(TestBed.runInInjectionContext(() => verifiedGuard(mockRoute, mockState)))).toBe(
        true,
      );
    });
  });

  // The role guards now answer the session question the same way the two above do. These
  // cases are generated over both guards rather than written out twice, because the bug
  // they exist to catch was precisely that these two drifted apart from the other two —
  // and a shared list is harder to add a third guard to and forget.
  const roleGuards = [
    { name: 'ownerGuard', guard: ownerGuard, role: 'Owner' as UserRole },
    { name: 'adminGuard', guard: adminGuard, role: 'Admin' as UserRole },
  ];

  for (const { name, guard, role } of roleGuards) {
    describe(name, () => {
      it('should allow navigation when the authenticated user has the role', () => {
        isAuthSignal.set(true);
        userRoleSignal.set(role);

        expect(settle(TestBed.runInInjectionContext(() => guard(mockRoute, mockState)))).toBe(true);
      });

      it('should redirect home when the role is wrong', () => {
        isAuthSignal.set(true);
        userRoleSignal.set('Seeker');

        expect(settle(TestBed.runInInjectionContext(() => guard(mockRoute, mockState)))).toEqual(
          homeUrlTree,
        );
      });

      /**
       * The regression this was written for. An expired access token is not a signed-out
       * person: the refresh token is usually still good. Before the fix these two guards
       * did not know that, and an owner returning the next day was redirected off their
       * own dashboard onto the design system.
       */
      it('should silently refresh when the access token expired but the refresh token is good', () => {
        isAuthSignal.set(false);
        userRoleSignal.set(role);
        mockAuthService.refreshToken = 'valid-refresh-token';
        mockAuthService.refreshSession.mockReturnValue(of({ accessToken: 'new-token' }));

        expect(settle(TestBed.runInInjectionContext(() => guard(mockRoute, mockState)))).toBe(true);
        expect(mockAuthService.refreshSession).toHaveBeenCalled();
      });

      /**
       * Signed out is a different answer from wrong role. Home would look like the site
       * had simply refused; /login says what to do about it, and comes back afterwards.
       */
      it('should redirect to /login with returnUrl when signed out entirely', () => {
        isAuthSignal.set(false);
        userRoleSignal.set(null);
        mockAuthService.refreshToken = null;

        expect(settle(TestBed.runInInjectionContext(() => guard(mockRoute, mockState)))).toEqual(
          loginUrlTree,
        );
      });

      it('should redirect to /login when the refresh token is spent', () => {
        isAuthSignal.set(false);
        userRoleSignal.set(role);
        mockAuthService.refreshToken = 'bad-refresh-token';
        mockAuthService.refreshSession.mockReturnValue(
          throwError(() => new Error('Refresh failed')),
        );

        expect(settle(TestBed.runInInjectionContext(() => guard(mockRoute, mockState)))).toEqual(
          loginUrlTree,
        );
      });
    });
  }
});
