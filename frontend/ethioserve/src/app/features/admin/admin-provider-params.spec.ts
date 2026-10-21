import {
  DEFAULT_STATUS,
  STATUS_FILTERS,
  parseQueueParams,
  queueToQueryParams,
  withPage,
  withSearch,
  withStatus,
} from './admin-provider-params';

const source = (record: Record<string, string>) => ({ get: (name: string): string | null => record[name] ?? null });

describe('parseQueueParams', () => {
  it('opens on the pending queue', () => {
    expect(parseQueueParams(source({})).status).toBe('Pending');
    expect(DEFAULT_STATUS).toBe('Pending');
  });

  it('reads valid values', () => {
    expect(parseQueueParams(source({ status: 'Verified', search: '  abebe ', page: '3' }))).toEqual({
      status: 'Verified',
      search: 'abebe',
      page: 3,
    });
  });

  it('falls back to the default for unknown or malformed values', () => {
    expect(parseQueueParams(source({ status: 'banana', page: '-2' }))).toEqual({ status: 'Pending' });
    expect(parseQueueParams(source({ page: '0' })).page).toBeUndefined();
  });

  it('limits the search text to 100 characters', () => {
    expect(parseQueueParams(source({ search: 'x'.repeat(150) })).search).toHaveLength(100);
  });

  it('knows every status plus All', () => {
    expect([...STATUS_FILTERS].sort()).toEqual(['All', 'Pending', 'Rejected', 'Suspended', 'Unverified', 'Verified']);
  });
});

describe('queueToQueryParams', () => {
  it('leaves the defaults out of the URL', () => {
    expect(queueToQueryParams({ status: 'Pending', page: 1 })).toEqual({});
  });

  it('includes everything that differs from the defaults, including All', () => {
    expect(queueToQueryParams({ status: 'All', search: 'abebe', page: 2 })).toEqual({
      status: 'All',
      search: 'abebe',
      page: '2',
    });
  });

  it('survives a round trip', () => {
    const params = { status: 'Suspended' as const, search: 'abebe', page: 4 };

    expect(parseQueueParams(source(queueToQueryParams(params)))).toEqual(params);
  });
});

describe('filter changes', () => {
  it('go back to the first page', () => {
    expect(withStatus({ status: 'Pending', page: 3 }, 'Verified')).toEqual({ status: 'Verified', page: undefined });
    expect(withSearch({ status: 'Pending', page: 3 }, ' abebe ')).toEqual({ status: 'Pending', search: 'abebe', page: undefined });
  });

  it('ignore an unknown status', () => {
    expect(withStatus({ status: 'Verified' }, 'nope').status).toBe('Pending');
  });

  it('clear the search when it is blank', () => {
    expect(withSearch({ status: 'Pending', search: 'abebe' }, '   ').search).toBeUndefined();
  });

  it('change only the page when paging', () => {
    expect(withPage({ status: 'Verified', search: 'abebe' }, 5)).toEqual({ status: 'Verified', search: 'abebe', page: 5 });
  });
});