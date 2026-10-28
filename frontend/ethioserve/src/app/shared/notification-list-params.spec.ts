import {
  DEFAULT_NOTIFICATION_PARAMS,
  notificationParamsToQuery,
  parseNotificationParams,
  withNotificationPage,
} from './notification-list-params';

const source = (record: Record<string, string>) => ({ get: (name: string): string | null => record[name] ?? null });

describe('parseNotificationParams', () => {
  it('shows all notifications by default', () => {
    expect(parseNotificationParams(source({}))).toEqual({ unreadOnly: false });
    expect(DEFAULT_NOTIFICATION_PARAMS.unreadOnly).toBe(false);
  });

  it('reads the unread tab and a page', () => {
    expect(parseNotificationParams(source({ unread: 'true', page: '3' }))).toEqual({ unreadOnly: true, page: 3 });
  });

  it('falls back to the defaults for anything unknown or malformed', () => {
    expect(parseNotificationParams(source({ unread: 'yes', page: '-2' }))).toEqual({ unreadOnly: false });
    expect(parseNotificationParams(source({ page: '0' })).page).toBeUndefined();
  });
});

describe('notificationParamsToQuery', () => {
  it('leaves the defaults out of the URL and survives a round trip', () => {
    expect(notificationParamsToQuery({ unreadOnly: false, page: 1 })).toEqual({});

    const params = { unreadOnly: true, page: 4 };

    expect(parseNotificationParams(source(notificationParamsToQuery(params)))).toEqual(params);
  });
});

describe('withNotificationPage', () => {
  it('changes only the page', () => {
    expect(withNotificationPage({ unreadOnly: true }, 2)).toEqual({ unreadOnly: true, page: 2 });
  });
});