export interface ParamSource {
  get(name: string): string | null;
}

export interface FeedParams {
  serviceId?: string;
  page?: number;
}

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/** Reads the URL. A malformed id or page is ignored instead of reaching the API. */
export function parseFeedParams(source: ParamSource): FeedParams {
  const serviceId = source.get('serviceId');
  const pageText = source.get('page');

  return {
    serviceId: serviceId !== null && GUID.test(serviceId) ? serviceId.toLowerCase() : undefined,
    page: pageText !== null && /^\d{1,6}$/.test(pageText) && Number(pageText) > 0 ? Number(pageText) : undefined,
  };
}

export function feedToQuery(params: FeedParams): Record<string, string> {
  const query: Record<string, string> = {};

  if (params.serviceId) {
    query['serviceId'] = params.serviceId;
  }

  if (params.page && params.page > 1) {
    query['page'] = String(params.page);
  }

  return query;
}

export function withService(params: FeedParams, raw: string): FeedParams {
  return { ...params, serviceId: GUID.test(raw) ? raw.toLowerCase() : undefined, page: undefined };
}

export function withFeedPage(params: FeedParams, page: number): FeedParams {
  return { ...params, page };
}