import { DEFAULT_FILTER, REQUEST_STATUSES, listToQuery, parseListParams, withPage, withStatus } from './request-list-params';

const source = (record: Record<string, string>) => ({ get: (name: string): string | null => record[name] ?? null });

describe('parseListParams', () => {
  it('shows all requests by default', () => {
    expect(parseListParams(source({}))).toEqual({ status: 'All' });
    expect(DEFAULT_FILTER).toBe('All');
  });

  it('reads a valid status and page', () => {
    expect(parseListParams(source({ status: 'Quoted', page: '3' }))).toEqual({ status: 'Quoted', page: 3 });
  });

  it('falls back to the defaults for anything unknown or malformed', () => {
    expect(parseListParams(source({ status: 'banana', page: '-2' }))).toEqual({ status: 'All' });
    expect(parseListParams(source({ page: '0' })).page).toBeUndefined();
  });

  it('knows every API status', () => {
    expect([...REQUEST_STATUSES].sort()).toEqual(
      ['Accepted', 'Cancelled', 'Completed', 'InProgress', 'Pending', 'Quoted', 'Rejected', 'Scheduled'],
    );
  });
});

describe('listToQuery', () => {
  it('leaves the defaults out of the URL', () => {
    expect(listToQuery({ status: 'All', page: 1 })).toEqual({});
  });

  it('survives a round trip', () => {
    const params = { status: 'Cancelled' as const, page: 4 };

    expect(parseListParams(source(listToQuery(params)))).toEqual(params);
  });
});

describe('filter changes', () => {
  it('go back to the first page and ignore unknown statuses', () => {
    expect(withStatus({ status: 'All', page: 3 }, 'Pending')).toEqual({ status: 'Pending', page: undefined });
    expect(withStatus({ status: 'Pending' }, 'nope').status).toBe('All');
  });

  it('change only the page when paging', () => {
    expect(withPage({ status: 'Pending' }, 5)).toEqual({ status: 'Pending', page: 5 });
  });
});