import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AdminApi } from './admin.api';

const EMPTY_PAGE = { items: [], page: 1, pageSize: 15, totalCount: 0, totalPages: 0 };

describe('AdminApi', () => {
  let api: AdminApi;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(AdminApi);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('asks for the queue with only the filters that are set', () => {
    api.providers({ status: 'Pending', search: 'abebe', page: 2 }).subscribe();

    const request = httpMock.expectOne((r) => r.url === '/api/admin/providers');

    expect(request.request.params.get('status')).toBe('Pending');
    expect(request.request.params.get('search')).toBe('abebe');
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('15');
    request.flush(EMPTY_PAGE);
  });

  it('does not send a status filter for All, and leaves out page 1', () => {
    api.providers({ status: 'All', page: 1 }).subscribe();

    const request = httpMock.expectOne((r) => r.url === '/api/admin/providers');

    expect(request.request.params.has('status')).toBe(false);
    expect(request.request.params.has('page')).toBe(false);
    request.flush(EMPTY_PAGE);
  });

  it('uses the requested page size', () => {
    api.providers({}, 1).subscribe();

    const request = httpMock.expectOne((r) => r.url === '/api/admin/providers');

    expect(request.request.params.get('pageSize')).toBe('1');
    request.flush(EMPTY_PAGE);
  });

  it('posts every decision to its own route with the right body', () => {
    api.approve('p1').subscribe();
    const approve = httpMock.expectOne('/api/admin/providers/p1/approve');
    expect(approve.request.method).toBe('POST');
    expect(approve.request.body).toBeNull();
    approve.flush({});

    api.reject('p1', 'Unclear details').subscribe();
    const reject = httpMock.expectOne('/api/admin/providers/p1/reject');
    expect(reject.request.body).toEqual({ reason: 'Unclear details' });
    reject.flush({});

    api.suspend('p1', 'Complaints').subscribe();
    const suspend = httpMock.expectOne('/api/admin/providers/p1/suspend');
    expect(suspend.request.body).toEqual({ reason: 'Complaints' });
    suspend.flush({});

    api.reinstate('p1').subscribe();
    const reinstate = httpMock.expectOne('/api/admin/providers/p1/reinstate');
    expect(reinstate.request.body).toBeNull();
    reinstate.flush({});
  });

  it('encodes the provider id', () => {
    api.provider('a/b').subscribe();

    httpMock.expectOne('/api/admin/providers/a%2Fb').flush({});
  });
});