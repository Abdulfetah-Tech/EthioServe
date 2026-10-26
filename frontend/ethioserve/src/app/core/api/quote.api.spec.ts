import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { QuoteApi } from './quote.api';

const EMPTY_PAGE = { items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0 };

describe('QuoteApi', () => {
  let api: QuoteApi;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(QuoteApi);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('sends a quote to the request it is for, with the four fields the API accepts', () => {
    const body = { amount: 450, description: 'Parts and labour included.', estimatedDurationMinutes: 120, validForDays: 7 };

    api.submit('r1', body).subscribe();

    const request = httpMock.expectOne('/api/providers/me/requests/r1/quotes');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(body);
    request.flush({});
  });

  it('lists my quotes for one request, leaving out filters that are not set', () => {
    api.mine({ requestId: 'r1', page: 2 }, 20).subscribe();

    const request = httpMock.expectOne((r) => r.url === '/api/providers/me/quotes');

    expect(request.request.params.get('requestId')).toBe('r1');
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('20');
    request.flush(EMPTY_PAGE);
  });

  it('sends only the page size when nothing else is set', () => {
    api.mine({ page: 1 }).subscribe();

    const request = httpMock.expectOne((r) => r.url === '/api/providers/me/quotes');

    expect(request.request.params.has('requestId')).toBe(false);
    expect(request.request.params.has('page')).toBe(false);
    expect(request.request.params.get('pageSize')).toBe('10');
    request.flush(EMPTY_PAGE);
  });

  it('reads and withdraws one quote', () => {
    api.get('q1').subscribe();
    expect(httpMock.expectOne('/api/providers/me/quotes/q1').request.method).toBe('GET');
  });

  it('withdraws with POST and no body', () => {
    api.withdraw('q1').subscribe();

    const request = httpMock.expectOne('/api/providers/me/quotes/q1/withdraw');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toBeNull();
    request.flush({});
  });

  it('lists the quotes on a request for the customer', () => {
    api.forRequest('r1').subscribe();

    const request = httpMock.expectOne('/api/requests/r1/quotes');
    expect(request.request.method).toBe('GET');
    request.flush([]);
  });

  it('accepts and declines with POST and no body', () => {
    api.accept('r1', 'q1').subscribe();
    const accept = httpMock.expectOne('/api/requests/r1/quotes/q1/accept');
    expect(accept.request.method).toBe('POST');
    expect(accept.request.body).toBeNull();
    accept.flush({});

    api.decline('r1', 'q1').subscribe();
    const decline = httpMock.expectOne('/api/requests/r1/quotes/q1/decline');
    expect(decline.request.method).toBe('POST');
    expect(decline.request.body).toBeNull();
    decline.flush({});
  });

  it('encodes ids in the URLs', () => {
    api.accept('a/b', 'c/d').subscribe();

    httpMock.expectOne('/api/requests/a%2Fb/quotes/c%2Fd/accept').flush({});
  });
});