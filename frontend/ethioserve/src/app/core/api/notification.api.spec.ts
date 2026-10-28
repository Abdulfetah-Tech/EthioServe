import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { NotificationApi } from './notification.api';

const EMPTY_PAGE = { items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 };

describe('NotificationApi', () => {
  let api: NotificationApi;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(NotificationApi);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('lists with only the filters that are set', () => {
    api.list({ unreadOnly: true, page: 2 }, 8).subscribe();

    const request = httpMock.expectOne((r) => r.url === '/api/notifications');

    expect(request.request.params.get('unreadOnly')).toBe('true');
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('8');
    request.flush(EMPTY_PAGE);
  });

  it('leaves out unreadOnly and page 1 when they are the defaults', () => {
    api.list({ unreadOnly: false, page: 1 }).subscribe();

    const request = httpMock.expectOne((r) => r.url === '/api/notifications');

    expect(request.request.params.has('unreadOnly')).toBe(false);
    expect(request.request.params.has('page')).toBe(false);
    expect(request.request.params.get('pageSize')).toBe('20');
    request.flush(EMPTY_PAGE);
  });

  it('reads the unread count as a number', () => {
    let count = -1;

    api.unreadCount().subscribe((value) => (count = value));
    httpMock.expectOne('/api/notifications/unread-count').flush({ count: 7 });

    expect(count).toBe(7);
  });

  it('marks one read with POST and no body', () => {
    api.markRead('n1').subscribe();

    const request = httpMock.expectOne('/api/notifications/n1/read');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toBeNull();
    request.flush(null);
  });

  it('marks all read and reports how many changed', () => {
    let updated = -1;

    api.markAllRead().subscribe((value) => (updated = value));
    const request = httpMock.expectOne('/api/notifications/read-all');
    expect(request.request.method).toBe('POST');
    request.flush({ updated: 3 });

    expect(updated).toBe(3);
  });

  it('encodes the id in the URL', () => {
    api.markRead('a/b').subscribe();

    httpMock.expectOne('/api/notifications/a%2Fb/read').flush(null);
  });
});