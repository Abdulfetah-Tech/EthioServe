import {
  parseServiceParams,
  serviceParamsToQuery,
  withCategoryFilter,
  withInactive,
  withServicePage,
  withServiceSearch,
} from './admin-service-params';

const CATEGORY = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';

const source = (record: Record<string, string>) => ({ get: (name: string): string | null => record[name] ?? null });

describe('parseServiceParams', () => {
  it('shows inactive services by default', () => {
    expect(parseServiceParams(source({})).includeInactive).toBe(true);
  });

  it('reads valid values', () => {
    expect(parseServiceParams(source({ categoryId: CATEGORY.toUpperCase(), search: ' pipe ', includeInactive: 'false', page: '2' }))).toEqual({
      categoryId: CATEGORY,
      search: 'pipe',
      includeInactive: false,
      page: 2,
    });
  });

  it('ignores a malformed category or page', () => {
    const result = parseServiceParams(source({ categoryId: '<script>', page: '-3' }));

    expect(result.categoryId).toBeUndefined();
    expect(result.page).toBeUndefined();
  });

  it('limits the search text to 100 characters', () => {
    expect(parseServiceParams(source({ search: 'x'.repeat(150) })).search).toHaveLength(100);
  });
});

describe('serviceParamsToQuery', () => {
  it('leaves the defaults out of the URL', () => {
    expect(serviceParamsToQuery({ includeInactive: true, page: 1 })).toEqual({});
  });

  it('survives a round trip', () => {
    const params = { categoryId: CATEGORY, search: 'pipe', includeInactive: false, page: 3 };

    expect(parseServiceParams(source(serviceParamsToQuery(params)))).toEqual(params);
  });
});

describe('filter changes', () => {
  it('go back to the first page', () => {
    expect(withCategoryFilter({ includeInactive: true, page: 4 }, CATEGORY)).toEqual({
      categoryId: CATEGORY,
      includeInactive: true,
      page: undefined,
    });
    expect(withServiceSearch({ includeInactive: true, page: 4 }, ' x ').page).toBeUndefined();
    expect(withInactive({ includeInactive: true, page: 4 }, false)).toEqual({ includeInactive: false, page: undefined });
  });

  it('clear the category when the value is empty or malformed', () => {
    expect(withCategoryFilter({ includeInactive: true, categoryId: CATEGORY }, '').categoryId).toBeUndefined();
    expect(withCategoryFilter({ includeInactive: true }, 'nope').categoryId).toBeUndefined();
  });

  it('change only the page when paging', () => {
    expect(withServicePage({ includeInactive: false, search: 'pipe' }, 5)).toEqual({ includeInactive: false, search: 'pipe', page: 5 });
  });
});