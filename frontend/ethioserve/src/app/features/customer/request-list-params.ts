import { RequestStatus } from '../../core/models/request.models';

export const REQUEST_STATUSES: readonly RequestStatus[] = [
  'Pending',
  'Quoted',
  'Accepted',
  'Scheduled',
  'InProgress',
  'Completed',
  'Cancelled',
  'Rejected',
];

export type StatusFilter = RequestStatus | 'All';

export const DEFAULT_FILTER: StatusFilter = 'All';

export interface ParamSource {
  get(name: string): string | null;
}

export interface ListParams {
  status: StatusFilter;
  page?: number;
}

/** Reads the URL. Anything unknown or malformed falls back to the default instead of reaching the API. */
export function parseListParams(source: ParamSource): ListParams {
  const pageText = source.get('page');
  const page = pageText !== null && /^\d{1,6}$/.test(pageText) && Number(pageText) > 0 ? Number(pageText) : undefined;

  return {
    status: REQUEST_STATUSES.find((status) => status === source.get('status')) ?? DEFAULT_FILTER,
    page,
  };
}

export function listToQuery(params: ListParams): Record<string, string> {
  const query: Record<string, string> = {};

  if (params.status !== DEFAULT_FILTER) {
    query['status'] = params.status;
  }

  if (params.page && params.page > 1) {
    query['page'] = String(params.page);
  }

  return query;
}

export function withStatus(params: ListParams, raw: string): ListParams {
  return { ...params, status: REQUEST_STATUSES.find((status) => status === raw) ?? DEFAULT_FILTER, page: undefined };
}

export function withPage(params: ListParams, page: number): ListParams {
  return { ...params, page };
}