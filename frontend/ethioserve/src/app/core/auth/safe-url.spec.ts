import { safeReturnUrl } from './safe-url';

describe('safeReturnUrl', () => {
  it('accepts an in-app path, including its query string', () => {
    expect(safeReturnUrl('/customer/requests?page=2')).toBe('/customer/requests?page=2');
  });

  it.each([null, undefined, '', 'https://evil.example', '//evil.example', 'javascript:alert(1)', '/\\evil.example', 'customer'])(
    'rejects %s',
    (value) => {
      expect(safeReturnUrl(value)).toBeNull();
    },
  );
});