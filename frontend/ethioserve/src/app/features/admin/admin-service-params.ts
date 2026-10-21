import { ParamSource } from './admin-provider-params';

export interface ServiceListParams {
  categoryId?: string;
  search?: string;
  includeInactive: boolean;
  page?: number;
}

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const MAX_SEARCH_LENGTH = 100;

function asGuid(value: string | null): string | undefined {
  return value !== null && GUID.test(value) ? value.toLowerCase() : undefined;
}

/** Reads the URL. Inactive services are shown by default, because the admin needs to see what is switched off. */
export function parseServiceParams(source: ParamSource): ServiceListParams {
  const search = source.get('search')?.trim().slice(0, MAX_SEARCH_LENGTH);
  const pageText = source.get('page');

  return {
    categoryId: asGuid(source.get('categoryId')),
    search: search || undefined,
    includeInactive: source.get('includeInactive') !== 'false',
    page: pageText !== null && /^\d{1,6}$/.test(pageText) && Number(pageText) > 0 ? Number(pageText) : undefined,
  };
}

export function serviceParamsToQuery(params: ServiceListParams): Record<string, string> {
  const query: Record<string, string> = {};

  if (params.categoryId) {
    query['categoryId'] = params.categoryId;
  }

  if (params.search) {
    query['search'] = params.search;
  }

  if (!params.includeInactive) {
    query['includeInactive'] = 'false';
  }

  if (params.page && params.page > 1) {
    query['page'] = String(params.page);
  }

  return query;
}

export function withCategoryFilter(params: ServiceListParams, raw: string): ServiceListParams {
  return { ...params, categoryId: asGuid(raw), page: undefined };
}

export function withServiceSearch(params: ServiceListParams, term: string): ServiceListParams {
  return { ...params, search: term.trim().slice(0, MAX_SEARCH_LENGTH) || undefined, page: undefined };
}

export function withInactive(params: ServiceListParams, include: boolean): ServiceListParams {
  return { ...params, includeInactive: include, page: undefined };
}

export function withServicePage(params: ServiceListParams, page: number): ServiceListParams {
  return { ...params, page };
}