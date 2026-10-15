import { HttpTestingController } from '@angular/common/http/testing';
import { AuthService } from '../auth/auth.service';
import { AuthResponse, CurrentUser } from '../models/auth.models';

export function userFixture(overrides: Partial<CurrentUser> = {}): CurrentUser {
  return {
    id: 'user-1',
    email: 'abebe@test.local',
    displayName: 'Abebe Kebede',
    roles: ['Customer'],
    customerId: 'customer-1',
    providerId: null,
    ...overrides,
  };
}

export function authResponse(accessToken: string, user: CurrentUser = userFixture()): AuthResponse {
  return { accessToken, accessTokenExpiresAt: '2030-01-01T00:00:00Z', user };
}

/** Signs the service in through its real HTTP code path, answered by the testing backend. */
export function signIn(
  auth: AuthService,
  httpMock: HttpTestingController,
  token = 'token-1',
  roles: string[] = ['Customer'],
): void {
  auth.login({ email: 'abebe@test.local', password: 'Str0ngPassw0rd' }).subscribe();
  httpMock.expectOne('/api/auth/login').flush(authResponse(token, userFixture({ roles })));
}