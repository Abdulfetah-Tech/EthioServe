import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { LIVE_CONNECTION } from '../api/live-connection';
import { FakeConnection, notification } from '../testing/fake-connection';
import { AppNotification } from '../models/notification.models';
import { NotificationService } from './notification.service';

const page = (items: AppNotification[]) => ({ items, page: 1, pageSize: 8, totalCount: items.length, totalPages: 1 });

describe('NotificationService', () => {
  let service: NotificationService;
  let httpMock: HttpTestingController;
  let connection: FakeConnection;

  beforeEach(() => {
    connection = new FakeConnection();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: LIVE_CONNECTION, useValue: () => connection }],
    });
    service = TestBed.inject(NotificationService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    service.stop();
    httpMock.verify();
  });

  const answer = (count: number, items: AppNotification[]): void => {
    httpMock.expectOne('/api/notifications/unread-count').flush({ count });
    httpMock.expectOne((r) => r.url === '/api/notifications').flush(page(items));
  };

  const flushMicrotasks = async (): Promise<void> => {
    await Promise.resolve();
    await Promise.resolve();
  };

  it('loads the count and the newest notifications when it starts', () => {
    service.start();
    answer(2, [notification('a'), notification('b', { isRead: true })]);

    expect(service.unread()).toBe(2);
    expect(service.recent().map((n) => n.id)).toEqual(['a', 'b']);
  });

  it('starts only once, however often it is asked', async () => {
    service.start();
    service.start();
    answer(0, []);
    await flushMicrotasks();

    expect(connection.starts).toBe(1);
  });

  it('goes live once the connection is up', async () => {
    service.start();
    answer(0, []);
    expect(service.live()).toBe(false);

    await flushMicrotasks();

    expect(service.live()).toBe(true);
  });

  it('adds a pushed notification, counts it, and announces it', () => {
    service.start();
    answer(1, [notification('a')]);

    connection.emit(notification('b', { title: 'A time was proposed' }));

    expect(service.recent().map((n) => n.id)).toEqual(['b', 'a']);
    expect(service.unread()).toBe(2);
    expect(service.announcement()).toBe('New notification: A time was proposed');
  });

  it('ignores a pushed notification it already has', () => {
    service.start();
    answer(1, [notification('a')]);

    connection.emit(notification('a'));

    expect(service.recent()).toHaveLength(1);
    expect(service.unread()).toBe(1);
  });

  it('keeps only the newest eight', () => {
    service.start();
    answer(0, Array.from({ length: 8 }, (_, i) => notification(`old${i}`, { isRead: true })));

    connection.emit(notification('fresh'));

    expect(service.recent()).toHaveLength(8);
    expect(service.recent()[0].id).toBe('fresh');
  });

  it('marks one read at once, tells the server, and does not count twice', () => {
    service.start();
    answer(2, [notification('a'), notification('b')]);

    service.markRead(service.recent()[0]);

    expect(service.unread()).toBe(1);
    expect(service.recent()[0].isRead).toBe(true);
    const post = httpMock.expectOne('/api/notifications/a/read');
    expect(post.request.method).toBe('POST');
    post.flush(null);

    service.markRead(service.recent()[0]);

    expect(service.unread()).toBe(1);
    httpMock.expectNone('/api/notifications/a/read');
  });

  it('puts the real state back when the server refuses', () => {
    service.start();
    answer(1, [notification('a')]);

    service.markRead(service.recent()[0]);
    httpMock.expectOne('/api/notifications/a/read').flush({}, { status: 500, statusText: 'Error' });

    answer(1, [notification('a')]);

    expect(service.unread()).toBe(1);
    expect(service.recent()[0].isRead).toBe(false);
  });

  it('marks everything read', () => {
    service.start();
    answer(2, [notification('a'), notification('b')]);

    service.markAllRead();

    expect(service.unread()).toBe(0);
    expect(service.recent().every((n) => n.isRead)).toBe(true);
    httpMock.expectOne('/api/notifications/read-all').flush({ updated: 2 });
  });

  it('shows it is no longer live when the connection drops, and refreshes when it comes back', async () => {
    service.start();
    answer(0, []);
    await flushMicrotasks();

    connection.close();
    expect(service.live()).toBe(false);

    connection.reconnected();
    expect(service.live()).toBe(true);
    answer(1, [notification('missed')]);

    expect(service.unread()).toBe(1);
  });

  it('keeps working without a connection', async () => {
    connection.startError = new Error('no network');

    service.start();
    answer(1, [notification('a')]);
    await flushMicrotasks();

    expect(service.live()).toBe(false);
    expect(service.unread()).toBe(1);
  });

  it('forgets everything and closes the connection when it stops', async () => {
    service.start();
    answer(1, [notification('a')]);
    await flushMicrotasks();

    service.stop();

    expect(connection.stops).toBe(1);
    expect(service.unread()).toBe(0);
    expect(service.recent()).toEqual([]);
    expect(service.live()).toBe(false);

    connection.emit(notification('late'));

    expect(service.recent()).toEqual([]);
  });

  it('changes its version whenever the stored notifications may have changed', () => {
    service.start();
    answer(1, [notification('a')]);
    const before = service.version();

    connection.emit(notification('b'));

    expect(service.version()).toBeGreaterThan(before);
  });
});