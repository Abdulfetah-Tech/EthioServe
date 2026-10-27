import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AppointmentApi } from './appointment.api';

const EMPTY_PAGE = { items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0 };
const BODY = { startAt: '2026-12-01T11:00:00.000Z', notes: 'Bring spare pipes' };

describe('AppointmentApi', () => {
  let api: AppointmentApi;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(AppointmentApi);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('proposes a time for the job it is for, with only the two fields the API accepts', () => {
    api.schedule('r1', BODY).subscribe();

    const request = httpMock.expectOne('/api/providers/me/requests/r1/appointment');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(BODY);
    request.flush({});
  });

  it('lists the provider appointments, sending only what differs from the defaults', () => {
    api.providerList({ scope: 'History', requestId: 'r1', page: 2 }, 5).subscribe();

    const request = httpMock.expectOne((r) => r.url === '/api/providers/me/appointments');

    expect(request.request.params.get('scope')).toBe('History');
    expect(request.request.params.get('requestId')).toBe('r1');
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('5');
    request.flush(EMPTY_PAGE);
  });

  it('leaves out the scope for upcoming, the request filter and page 1', () => {
    api.providerList({ scope: 'Upcoming', page: 1 }).subscribe();

    const request = httpMock.expectOne((r) => r.url === '/api/providers/me/appointments');

    expect(request.request.params.has('scope')).toBe(false);
    expect(request.request.params.has('requestId')).toBe(false);
    expect(request.request.params.has('page')).toBe(false);
    expect(request.request.params.get('pageSize')).toBe('10');
    request.flush(EMPTY_PAGE);
  });

  it('reads one appointment and reschedules it with POST', () => {
    api.providerGet('a1').subscribe();
    expect(httpMock.expectOne('/api/providers/me/appointments/a1').request.method).toBe('GET');

    api.reschedule('a1', BODY).subscribe();
    const request = httpMock.expectOne('/api/providers/me/appointments/a1/reschedule');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(BODY);
    request.flush({});
  });

  it('cancels, starts and completes with POST and no body', () => {
    api.providerCancel('a1').subscribe();
    api.start('a1').subscribe();
    api.complete('a1').subscribe();

    for (const action of ['cancel', 'start', 'complete']) {
      const request = httpMock.expectOne(`/api/providers/me/appointments/a1/${action}`);
      expect(request.request.method).toBe('POST');
      expect(request.request.body).toBeNull();
      request.flush({});
    }
  });

  it('lists the customer appointments from their own endpoint', () => {
    api.customerList({ scope: 'History', requestId: 'r1' }).subscribe();

    const request = httpMock.expectOne((r) => r.url === '/api/appointments');

    expect(request.request.params.get('scope')).toBe('History');
    expect(request.request.params.get('requestId')).toBe('r1');
    request.flush(EMPTY_PAGE);
  });

  it('confirms and cancels as the customer with POST and no body', () => {
    api.confirm('a1').subscribe();
    const confirm = httpMock.expectOne('/api/appointments/a1/confirm');
    expect(confirm.request.method).toBe('POST');
    expect(confirm.request.body).toBeNull();
    confirm.flush({});

    api.customerCancel('a1').subscribe();
    const cancel = httpMock.expectOne('/api/appointments/a1/cancel');
    expect(cancel.request.method).toBe('POST');
    cancel.flush({});
  });

  it('encodes ids in the URLs', () => {
    api.confirm('a/b').subscribe();

    httpMock.expectOne('/api/appointments/a%2Fb/confirm').flush({});
  });
});