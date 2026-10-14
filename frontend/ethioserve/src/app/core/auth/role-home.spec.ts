import { homeRouteFor } from './role-home';

describe('homeRouteFor', () => {
  it('sends admins to the admin area even when they hold other roles', () => {
    expect(homeRouteFor(['Customer', 'Admin'])).toBe('/admin');
  });

  it('sends providers and customers to their own areas', () => {
    expect(homeRouteFor(['Provider'])).toBe('/provider');
    expect(homeRouteFor(['Customer'])).toBe('/customer');
  });

  it('falls back to the home page for unknown or missing roles', () => {
    expect(homeRouteFor([])).toBe('/');
    expect(homeRouteFor(['Stranger'])).toBe('/');
  });
});