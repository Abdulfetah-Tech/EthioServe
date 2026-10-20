import { DEFAULT_SORT, parseSearchParams, toQueryParams, withFilter, withPage } from './search-params';

const CATEGORY_A = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
const CATEGORY_B = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb';
const SERVICE = 'cccccccc-cccc-4ccc-8ccc-cccccccccccc';

const source = (record: Record<string, string>) => ({ get: (name: string): string | null => record[name] ?? null });

describe('parseSearchParams', () => {
  it('reads every valid value', () => {
    const result = parseSearchParams(
      source({
        search: '  pipes ',
        categoryId: CATEGORY_A.toUpperCase(),
        serviceId: SERVICE,
        regionId: '1',
        cityId: '2',
        minRating: '4.5',
        availableOnly: 'true',
        sort: 'Price',
        page: '3',
      }),
    );

    expect(result).toEqual({
      search: 'pipes',
      categoryId: CATEGORY_A,
      serviceId: SERVICE,
      regionId: 1,
      cityId: 2,
      minRating: 4.5,
      availableOnly: true,
      sort: 'Price',
      page: 3,
    });
  });

  it('ignores anything malformed instead of passing it to the API', () => {
    const result = parseSearchParams(
      source({
        categoryId: 'not-a-guid',
        serviceId: '<script>alert(1)</script>',
        regionId: '-1',
        cityId: 'abc',
        minRating: '9',
        availableOnly: 'yes',
        sort: 'banana',
        page: '0',
      }),
    );

    expect(result).toEqual({});
  });

  it('limits the search text to 100 characters', () => {
    expect(parseSearchParams(source({ search: 'x'.repeat(150) })).search).toHaveLength(100);
  });

  it('treats a blank search as no search', () => {
    expect(parseSearchParams(source({ search: '   ' })).search).toBeUndefined();
  });
});

describe('toQueryParams', () => {
  it('leaves defaults out of the URL', () => {
    expect(toQueryParams({ sort: DEFAULT_SORT, page: 1 })).toEqual({});
  });

  it('includes everything that differs from the defaults', () => {
    expect(
      toQueryParams({ search: 'abebe', categoryId: CATEGORY_A, regionId: 1, minRating: 4, availableOnly: true, sort: 'Name', page: 2 }),
    ).toEqual({
      search: 'abebe',
      categoryId: CATEGORY_A,
      regionId: '1',
      minRating: '4',
      availableOnly: 'true',
      sort: 'Name',
      page: '2',
    });
  });

  it('survives a round trip through the URL', () => {
    const params = { search: 'abebe', categoryId: CATEGORY_A, serviceId: SERVICE, cityId: 4, minRating: 3, sort: 'Experience' as const, page: 5 };

    expect(parseSearchParams(source(toQueryParams(params)))).toEqual(params);
  });
});

describe('withFilter', () => {
  it('sets a filter and goes back to the first page', () => {
    expect(withFilter({ page: 3 }, 'minRating', '4')).toEqual({ minRating: 4 });
  });

  it('removes a filter when the value is empty', () => {
    expect(withFilter({ availableOnly: true, search: 'abebe' }, 'availableOnly', '')).toEqual({ search: 'abebe' });
  });

  it('clears the service when the category changes', () => {
    expect(withFilter({ categoryId: CATEGORY_A, serviceId: SERVICE }, 'categoryId', CATEGORY_B)).toEqual({
      categoryId: CATEGORY_B,
    });
  });

  it('clears the city when the region changes', () => {
    expect(withFilter({ regionId: 1, cityId: 5 }, 'regionId', '2')).toEqual({ regionId: 2 });
  });

  it('drops an invalid value instead of keeping it', () => {
    expect(withFilter({ categoryId: CATEGORY_A }, 'categoryId', 'nope')).toEqual({});
  });
});

describe('withPage', () => {
  it('changes only the page', () => {
    expect(withPage({ search: 'abebe' }, 4)).toEqual({ search: 'abebe', page: 4 });
  });
});