import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  CanActivateFn,
  Router,
  RouterStateSnapshot,
  UrlTree,
  provideRouter,
} from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { signIn } from '../testing/fixtures';
import { authGuard, guestGuard, roleGuard } from './auth.guards';

describe('route guards', () => {
  let auth: AuthService;
  let httpMock: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });

    auth = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });

  afterEach(() => httpMock.verify());

  const run = (guard: CanActivateFn, url = '/customer') =>
    TestBed.runInInjectionContext(() => guard({} as ActivatedRouteSnapshot, { url } as RouterStateSnapshot));

  const target = (result: unknown): string => router.serializeUrl(result as UrlTree);

  describe('authGuard', () => {
    it('sends signed-out users to the login page and remembers where they were going', () => {
      expect(target(run(authGuard, '/customer'))).toBe('/login?returnUrl=%2Fcustomer');
    });

    it('lets signed-in users through', () => {
      signIn(auth, httpMock);

      expect(run(authGuard)).toBe(true);
    });
  });

  describe('guestGuard', () => {
    it('lets signed-out users see the login and register pages', () => {
      expect(run(guestGuard)).toBe(true);
    });

    it.each([
      ['Customer', '/customer'],
      ['Provider', '/provider'],
      ['Admin', '/admin'],
    ])('sends a signed-in %s to %s', (role, expected) => {
      signIn(auth, httpMock, 'token-1', [role]);

      expect(target(run(guestGuard))).toBe(expected);
    });
  });

  describe('roleGuard', () => {
    it('sends signed-out users to the login page', () => {
      expect(target(run(roleGuard('Admin'), '/admin'))).toBe('/login?returnUrl=%2Fadmin');
    });

    it('sends users without the role to the access-denied page', () => {
      signIn(auth, httpMock, 'token-1', ['Customer']);

      expect(target(run(roleGuard('Admin'), '/admin'))).toBe('/forbidden');
    });

    it('lets users with the role through', () => {
      signIn(auth, httpMock, 'token-1', ['Admin']);

      expect(run(roleGuard('Admin'), '/admin')).toBe(true);
    });
  });
});