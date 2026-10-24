import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { JobRequestBody } from '../models/request.models';
import { RequestApi } from './request.api';

const EMPTY_PAGE = { items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0 };

const BODY: JobRequestBody = {
  title: 'Leaking kitchen pipe',
  description: 'The pipe under the sink leaks and needs repair.',
  address: 'Bole',
  regionId: 1,
  cityId: 1,
  budgetMin: null,
  budgetMax: 800,
  preferredDate: null,
  preferredTime: null,
};

describe('RequestApi', () => {
  let api: RequestApi;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(RequestApi);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('creates a request with POST', () => {
    api.create({ serviceId: 's1', ...BODY }).subscribe();

    const request = httpMock.expectOne('/api/requests');
    expect(request.request.method).toBe('POST');
    expect(request.request.body.serviceId).toBe('s1');
    expect(request.request.body.title).toBe('Leaking kitchen pipe');
    request.flush({});
  });

  it('lists with only the filters that are set', () => {
    api.list({ status: 'Pending', page: 2 }, 5).subscribe();

    const request = httpMock.expectOne((r) => r.url === '/api/requests');

    expect(request.request.params.get('status')).toBe('Pending');
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('5');
    request.flush(EMPTY_PAGE);
  });

  it('leaves out the status and page 1 when they are the defaults', () => {
    api.list({ page: 1 }).subscribe();

    const request = httpMock.expectOne((r) => r.url === '/api/requests');

    expect(request.request.params.has('status')).toBe(false);
    expect(request.request.params.has('page')).toBe(false);
    expect(request.request.params.get('pageSize')).toBe('10');
    request.flush(EMPTY_PAGE);
  });

  it('reads a request by id with GET', () => {
    api.get('r1').subscribe();
    const request = httpMock.expectOne('/api/requests/r1');
    expect(request.request.method).toBe('GET');
    request.flush({});
  });

  it('updates a request with PUT and the editable fields only', () => {
    api.update('r1', BODY).subscribe();

    const request = httpMock.expectOne('/api/requests/r1');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual(BODY);
    request.flush({});
  });

  it('cancels with a reason, or with none', () => {
    api.cancel('r1', 'Found someone else').subscribe();
    const withReason = httpMock.expectOne('/api/requests/r1/cancel');
    expect(withReason.request.method).toBe('POST');
    expect(withReason.request.body).toEqual({ reason: 'Found someone else' });
    withReason.flush({});

    api.cancel('r1', null).subscribe();
    const without = httpMock.expectOne('/api/requests/r1/cancel');
    expect(without.request.body).toEqual({ reason: null });
    without.flush({});
  });

  it('encodes ids in the URL', () => {
    api.get('a/b').subscribe();

    httpMock.expectOne('/api/requests/a%2Fb').flush({});
  });

  it('reads the provider feed with a service filter', () => {
    api.feed({ serviceId: 's1', page: 3 }, 6).subscribe();

    const request = httpMock.expectOne((r) => r.url === '/api/providers/me/requests');

    expect(request.request.params.get('serviceId')).toBe('s1');
    expect(request.request.params.get('page')).toBe('3');
    expect(request.request.params.get('pageSize')).toBe('6');
    request.flush(EMPTY_PAGE);
  });

  it('reads one request from the feed', () => {
    api.feedDetail('r1').subscribe();

    httpMock.expectOne('/api/providers/me/requests/r1').flush({});
  });
});