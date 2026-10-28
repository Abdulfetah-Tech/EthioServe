import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { LIVE_CONNECTION } from '../core/api/live-connection';
import { AppNotification } from '../core/models/notification.models';
import { FakeConnection, notification } from '../core/testing/fake-connection';
import { NotificationBell } from './notification-bell';

const page = (items: AppNotification[]) => ({ items, page: 1, pageSize: 8, totalCount: items.length, totalPages: 1 });

describe('NotificationBell', () => {
  let httpMock: HttpTestingController;
  let connection: FakeConnection;
  let fixture: ComponentFixture<NotificationBell>;

  beforeEach(async () => {
    connection = new FakeConnection();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: '**', children: [] }]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: LIVE_CONNECTION, useValue: () => connection },
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(NotificationBell);
    await fixture.whenStable();
  });

  afterEach(() => {
    fixture.destroy();
    httpMock.verify();
  });

  const element = (): HTMLElement => fixture.nativeElement as HTMLElement;

  const answer = async (count: number, items: AppNotification[]): Promise<void> => {
    httpMock.expectOne('/api/notifications/unread-count').flush({ count });
    httpMock.expectOne((r) => r.url === '/api/notifications').flush(page(items));
    await fixture.whenStable();
  };

  const bellButton = (): HTMLButtonElement => element().querySelector('.bell-button') as HTMLButtonElement;

  const open = async (count: number, items: AppNotification[]): Promise<void> => {
    bellButton().click();
    await answer(count, items); // opening refreshes
  };

  it('shows no badge and says nothing is unread when everything is read', async () => {
    await answer(0, []);

    expect(element().querySelector('.bell-badge')).toBeNull();
    expect(bellButton().textContent).toContain('Notifications, none unread');
  });

  it('shows the unread count and describes it for a screen reader', async () => {
    await answer(3, [notification('a')]);

    expect(element().querySelector('.bell-badge')?.textContent?.trim()).toBe('3');
    expect(bellButton().textContent).toContain('Notifications, 3 unread');
  });

  it('caps the badge at 99+', async () => {
    await answer(250, []);

    expect(element().querySelector('.bell-badge')?.textContent?.trim()).toBe('99+');
  });

  it('opens a list of the newest notifications, and Escape closes it', async () => {
    await answer(1, [notification('a', { title: 'New quote received' })]);

    await open(1, [notification('a', { title: 'New quote received' })]);

    expect(bellButton().getAttribute('aria-expanded')).toBe('true');
    expect(element().querySelector('.bell-panel')?.textContent).toContain('New quote received');

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    await fixture.whenStable();

    expect(element().querySelector('.bell-panel')).toBeNull();
    expect(bellButton().getAttribute('aria-expanded')).toBe('false');
  });

  it('marks a notification read when it is opened, and the badge drops', async () => {
    await answer(1, [notification('a')]);
    await open(1, [notification('a')]);

    (element().querySelector('.bell-item') as HTMLButtonElement).click();

    const post = httpMock.expectOne('/api/notifications/a/read');
    expect(post.request.method).toBe('POST');
    post.flush(null);
    await fixture.whenStable();

    expect(element().querySelector('.bell-badge')).toBeNull();
    expect(element().querySelector('.bell-panel')).toBeNull();
  });

  it('marks everything read from the panel', async () => {
    await answer(2, [notification('a'), notification('b')]);
    await open(2, [notification('a'), notification('b')]);

    const markAll = Array.from(element().querySelectorAll('button')).find((b) => b.textContent?.trim() === 'Mark all as read');
    markAll?.click();

    httpMock.expectOne('/api/notifications/read-all').flush({ updated: 2 });
    await fixture.whenStable();

    expect(element().querySelector('.bell-badge')).toBeNull();
  });

  it('says so when there is nothing to show', async () => {
    await answer(0, []);
    await open(0, []);

    expect(element().querySelector('.bell-panel')?.textContent).toContain('You have no notifications yet');
  });

  it('updates the badge and announces a notification that is pushed live', async () => {
    await answer(0, []);

    connection.emit(notification('new', { title: 'A time was proposed' }));
    await fixture.whenStable();

    expect(element().querySelector('.bell-badge')?.textContent?.trim()).toBe('1');
    expect(element().querySelector('[role="status"]')?.textContent).toContain('New notification: A time was proposed');
  });

  it('links to the full page', async () => {
    await answer(0, []);
    await open(0, []);

    expect(element().querySelector('a[href="/notifications"]')).not.toBeNull();
  });
});