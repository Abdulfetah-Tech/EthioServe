import { feedToQuery, parseFeedParams, withFeedPage, withService } from './feed-params';

const SERVICE = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';

const source = (record: Record<string, string>) => ({ get: (name: string): string | null => record[name] ?? null });

describe('parseFeedParams', () => {
  it('has no filter by default', () => {
    expect(parseFeedParams(source({}))).toEqual({});
  });

  it('reads a valid service and page', () => {
    expect(parseFeedParams(source({ serviceId: SERVICE.toUpperCase(), page: '2' }))).toEqual({ serviceId: SERVICE, page: 2 });
  });

  it('ignores a malformed service or page instead of passing it on', () => {
    const result = parseFeedParams(source({ serviceId: '<script>', page: '-1' }));

    expect(result.serviceId).toBeUndefined();
    expect(result.page).toBeUndefined();
  });
});

describe('feedToQuery', () => {
  it('leaves page 1 out and survives a round trip', () => {
    expect(feedToQuery({ page: 1 })).toEqual({});

    const params = { serviceId: SERVICE, page: 3 };

    expect(parseFeedParams(source(feedToQuery(params)))).toEqual(params);
  });
});

describe('filter changes', () => {
  it('go back to the first page, and a blank or malformed service clears the filter', () => {
    expect(withService({ page: 4 }, SERVICE)).toEqual({ serviceId: SERVICE, page: undefined });
    expect(withService({ serviceId: SERVICE }, '').serviceId).toBeUndefined();
    expect(withService({ serviceId: SERVICE }, 'nope').serviceId).toBeUndefined();
  });

  it('change only the page when paging', () => {
    expect(withFeedPage({ serviceId: SERVICE }, 2)).toEqual({ serviceId: SERVICE, page: 2 });
  });
});