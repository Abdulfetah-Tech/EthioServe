import { HttpClient, HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { authResponse, signIn } from '../testing/fixtures';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

const REFRESH_URL = '/api/auth/refresh-token';
const unauthorized = { status: 401, statusText: 'Unauthorized' };

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let auth: AuthService;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
  });

  afterEach(() => httpMock.verify());

  it('adds the bearer token to API calls', () => {
    signIn(auth, httpMock, 'token-1');

    http.get('/api/things').subscribe();

    const request = httpMock.expectOne('/api/things');
    expect(request.request.headers.get('Authorization')).toBe('Bearer token-1');
    request.flush({});
  });

  it('never sends the token to other sites', () => {
    signIn(auth, httpMock, 'token-1');

    http.get('https://other.example/data').subscribe();

    const request = httpMock.expectOne('https://other.example/data');
    expect(request.request.headers.has('Authorization')).toBe(false);
    request.flush({});
  });

  it('does not try to refresh when the login endpoint itself answers 401', () => {
    let status = 0;

    http.post('/api/auth/login', {}).subscribe({ error: (error: HttpErrorResponse) => (status = error.status) });
    httpMock.expectOne('/api/auth/login').flush({}, unauthorized);

    expect(status).toBe(401);
    httpMock.expectNone(REFRESH_URL);
  });

  it('refreshes once and retries the request with the new token after a 401', () => {
    signIn(auth, httpMock, 'old-token');
    let body: unknown;

    http.get('/api/things').subscribe((value) => (body = value));

    httpMock.expectOne('/api/things').flush({}, unauthorized);

    const refresh = httpMock.expectOne(REFRESH_URL);
    expect(refresh.request.headers.get('X-Requested-With')).toBe('XMLHttpRequest');
    refresh.flush(authResponse('new-token'));

    const retry = httpMock.expectOne('/api/things');
    expect(retry.request.headers.get('Authorization')).toBe('Bearer new-token');
    retry.flush({ ok: true });

    expect(body).toEqual({ ok: true });
    expect(auth.accessToken()).toBe('new-token');
  });

  it('five parallel 401s cause exactly one refresh', () => {
    signIn(auth, httpMock, 'old-token');
    const results: unknown[] = [];

    for (let i = 1; i <= 5; i++) {
      http.get(`/api/things/${i}`).subscribe((value) => results.push(value));
    }

    for (let i = 1; i <= 5; i++) {
      httpMock.expectOne(`/api/things/${i}`).flush({}, unauthorized);
    }

    const refreshes = httpMock.match(REFRESH_URL);
    expect(refreshes).toHaveLength(1);
    refreshes[0].flush(authResponse('new-token'));

    for (let i = 1; i <= 5; i++) {
      const retry = httpMock.expectOne(`/api/things/${i}`);
      expect(retry.request.headers.get('Authorization')).toBe('Bearer new-token');
      retry.flush({ id: i });
    }

    expect(results).toHaveLength(5);
  });

  it('signs the user out and goes to the login page when the refresh fails', () => {
    signIn(auth, httpMock, 'old-token');
    let failed = false;

    http.get('/api/things').subscribe({ error: () => (failed = true) });

    httpMock.expectOne('/api/things').flush({}, unauthorized);
    httpMock.expectOne(REFRESH_URL).flush({}, unauthorized);

    expect(failed).toBe(true);
    expect(auth.isAuthenticated()).toBe(false);
    expect(auth.accessToken()).toBeNull();
    expect(router.navigate).toHaveBeenCalledWith(['/login'], { queryParams: { returnUrl: router.url } });
  });

  it('does not loop when the retried request is rejected again', () => {
    signIn(auth, httpMock, 'old-token');
    let status = 0;

    http.get('/api/things').subscribe({ error: (error: HttpErrorResponse) => (status = error.status) });

    httpMock.expectOne('/api/things').flush({}, unauthorized);
    httpMock.expectOne(REFRESH_URL).flush(authResponse('new-token'));
    httpMock.expectOne('/api/things').flush({}, unauthorized);

    expect(status).toBe(401);
    httpMock.expectNone(REFRESH_URL);
    expect(auth.isAuthenticated()).toBe(true); // a rejected retry is not a reason to end the session
  });

  it.each([400, 403, 404, 500])('does not refresh for a %s response', (status) => {
    signIn(auth, httpMock, 'token-1');
    let received = 0;

    http.get('/api/things').subscribe({ error: (error: HttpErrorResponse) => (received = error.status) });
    httpMock.expectOne('/api/things').flush({}, { status, statusText: 'Error' });

    expect(received).toBe(status);
    httpMock.expectNone(REFRESH_URL);
    expect(auth.isAuthenticated()).toBe(true);
  });
});