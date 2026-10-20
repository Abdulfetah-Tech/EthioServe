import { ProviderSearchParams, ProviderSort } from '../../core/models/marketplace.models';

export const DEFAULT_SORT: ProviderSort = 'Rating';

export const PROVIDER_SORTS: readonly ProviderSort[] = ['Rating', 'Experience', 'Price', 'Name'];

export const SORT_OPTIONS: readonly { value: ProviderSort; label: string }[] = [
  { value: 'Rating', label: 'Highest rated' },
  { value: 'Experience', label: 'Most experienced' },
  { value: 'Price', label: 'Lowest price' },
  { value: 'Name', label: 'Name (A to Z)' },
];

export const RATING_OPTIONS: readonly { value: string; label: string }[] = [
  { value: '', label: 'Any rating' },
  { value: '4.5', label: '4.5 and up' },
  { value: '4', label: '4 and up' },
  { value: '3', label: '3 and up' },
];

export type FilterKey = 'search' | 'categoryId' | 'serviceId' | 'regionId' | 'cityId' | 'minRating' | 'availableOnly' | 'sort';

/** Anything with a get(name) method: Angular's ParamMap, or a plain record wrapped by the caller. */
export interface ParamSource {
  get(name: string): string | null;
}

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const MAX_SEARCH_LENGTH = 100;

function positiveInt(value: string | null): number | undefined {
  if (value === null || !/^\d{1,9}$/.test(value)) {
    return undefined;
  }

  const parsed = Number(value);

  return parsed > 0 ? parsed : undefined;
}

function guid(value: string | null): string | undefined {
  return value !== null && GUID.test(value) ? value.toLowerCase() : undefined;
}

function rating(value: string | null): number | undefined {
  if (value === null || !/^\d(\.\d)?$/.test(value)) {
    return undefined;
  }

  const parsed = Number(value);

  return parsed >= 1 && parsed <= 5 ? parsed : undefined;
}

/**
 * Reads the URL into search parameters. Every value is validated, so a hand-edited or malicious URL can never
 * reach the API as anything other than a well-formed id, number or known sort name.
 */
export function parseSearchParams(source: ParamSource): ProviderSearchParams {
  const search = source.get('search')?.trim().slice(0, MAX_SEARCH_LENGTH);

  return {
    search: search || undefined,
    categoryId: guid(source.get('categoryId')),
    serviceId: guid(source.get('serviceId')),
    regionId: positiveInt(source.get('regionId')),
    cityId: positiveInt(source.get('cityId')),
    minRating: rating(source.get('minRating')),
    availableOnly: source.get('availableOnly') === 'true' ? true : undefined,
    sort: PROVIDER_SORTS.find((sort) => sort === source.get('sort')),
    page: positiveInt(source.get('page')),
  };
}

/** The URL for a set of parameters. Defaults are left out, so the address bar stays short and clean. */
export function toQueryParams(params: ProviderSearchParams): Record<string, string> {
  const query: Record<string, string> = {};

  if (params.search) {
    query['search'] = params.search;
  }

  if (params.categoryId) {
    query['categoryId'] = params.categoryId;
  }

  if (params.serviceId) {
    query['serviceId'] = params.serviceId;
  }

  if (params.regionId) {
    query['regionId'] = String(params.regionId);
  }

  if (params.cityId) {
    query['cityId'] = String(params.cityId);
  }

  if (params.minRating) {
    query['minRating'] = String(params.minRating);
  }

  if (params.availableOnly) {
    query['availableOnly'] = 'true';
  }

  if (params.sort && params.sort !== DEFAULT_SORT) {
    query['sort'] = params.sort;
  }

  if (params.page && params.page > 1) {
    query['page'] = String(params.page);
  }

  return query;
}

/**
 * Changes one filter. An empty value removes it, the page goes back to 1, and dependent filters are cleared
 * (a new category clears the service; a new region clears the city).
 */
export function withFilter(params: ProviderSearchParams, key: FilterKey, raw: string): ProviderSearchParams {
  const record: Record<string, string> = { ...toQueryParams(params) };

  if (raw === '' || raw === 'false') {
    delete record[key];
  } else {
    record[key] = raw;
  }

  delete record['page'];

  if (key === 'categoryId') {
    delete record['serviceId'];
  }

  if (key === 'regionId') {
    delete record['cityId'];
  }

  return parseSearchParams({ get: (name: string): string | null => record[name] ?? null });
}

export function withPage(params: ProviderSearchParams, page: number): ProviderSearchParams {
  return { ...params, page };
}