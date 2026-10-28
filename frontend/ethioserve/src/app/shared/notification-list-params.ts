export interface ParamSource {
  get(name: string): string | null;
}

export interface NotificationListParams {
  unreadOnly: boolean;
  page?: number;
}

export const DEFAULT_NOTIFICATION_PARAMS: NotificationListParams = { unreadOnly: false };

/** Reads the URL. Anything unknown or malformed falls back to the default instead of reaching the API. */
export function parseNotificationParams(source: ParamSource): NotificationListParams {
  const pageText = source.get('page');

  return {
    unreadOnly: source.get('unread') === 'true',
    page: pageText !== null && /^\d{1,6}$/.test(pageText) && Number(pageText) > 0 ? Number(pageText) : undefined,
  };
}

export function notificationParamsToQuery(params: NotificationListParams): Record<string, string> {
  const query: Record<string, string> = {};

  if (params.unreadOnly) {
    query['unread'] = 'true';
  }

  if (params.page && params.page > 1) {
    query['page'] = String(params.page);
  }

  return query;
}

export function withNotificationPage(params: NotificationListParams, page: number): NotificationListParams {
  return { ...params, page };
}