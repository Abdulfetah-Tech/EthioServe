import { StatusFilter } from '../../core/models/admin.models';

export const STATUS_FILTERS: readonly StatusFilter[] = ['Pending', 'Verified', 'Rejected', 'Suspended', 'Unverified', 'All'];

/** A review queue opens on the work that is waiting. */
export const DEFAULT_STATUS: StatusFilter = 'Pending';

export interface ParamSource {
  get(name: string): string | null;
}

export interface QueueParams {
  status: StatusFilter;
  search?: string;
  page?: number;
}

const MAX_SEARCH_LENGTH = 100;

/** Reads the URL. Anything unknown or malformed falls back to the default instead of reaching the API. */
export function parseQueueParams(source: ParamSource): QueueParams {
  const search = source.get('search')?.trim().slice(0, MAX_SEARCH_LENGTH);
  const pageText = source.get('page');
  const page = pageText !== null && /^\d{1,6}$/.test(pageText) && Number(pageText) > 0 ? Number(pageText) : undefined;

  return {
    status: STATUS_FILTERS.find((status) => status === source.get('status')) ?? DEFAULT_STATUS,
    search: search || undefined,
    page,
  };
}

export function queueToQueryParams(params: QueueParams): Record<string, string> {
  const query: Record<string, string> = {};

  if (params.status !== DEFAULT_STATUS) {
    query['status'] = params.status;
  }

  if (params.search) {
    query['search'] = params.search;
  }

  if (params.page && params.page > 1) {
    query['page'] = String(params.page);
  }

  return query;
}

export function withStatus(params: QueueParams, raw: string): QueueParams {
  return { ...params, status: STATUS_FILTERS.find((status) => status === raw) ?? DEFAULT_STATUS, page: undefined };
}

export function withSearch(params: QueueParams, term: string): QueueParams {
  return { ...params, search: term.trim().slice(0, MAX_SEARCH_LENGTH) || undefined, page: undefined };
}

export function withPage(params: QueueParams, page: number): QueueParams {
  return { ...params, page };
}