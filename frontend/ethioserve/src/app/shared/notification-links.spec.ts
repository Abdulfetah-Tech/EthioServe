import { notificationRoute } from './notification-links';

const ID = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
const link = (relatedEntityType: string | null, relatedEntityId: string | null = ID) => ({ relatedEntityType, relatedEntityId });

describe('notificationRoute', () => {
  it('sends a provider to the request in their feed and a customer to their own request', () => {
    expect(notificationRoute(link('ServiceRequest'), ['Provider'])).toBe(`/provider/requests/${ID}`);
    expect(notificationRoute(link('ServiceRequest'), ['Customer'])).toBe(`/customer/requests/${ID}`);
  });

  it('sends a provider to the quote', () => {
    expect(notificationRoute(link('Quote'), ['Provider'])).toBe(`/provider/quotes/${ID}`);
    expect(notificationRoute(link('Quote'), ['Customer'])).toBeNull();
  });

  it('sends each side to their appointments', () => {
    expect(notificationRoute(link('Appointment'), ['Provider'])).toBe(`/provider/appointments/${ID}`);
    expect(notificationRoute(link('Appointment', null), ['Provider'])).toBe('/provider/appointments');
    expect(notificationRoute(link('Appointment'), ['Customer'])).toBe('/customer/appointments');
  });

  it('sends a provider decision to the provider overview, whatever the id', () => {
    expect(notificationRoute(link('ProviderProfile'), ['Provider'])).toBe('/provider');
    expect(notificationRoute(link('ProviderProfile', null), ['Provider'])).toBe('/provider');
    expect(notificationRoute(link('ProviderProfile'), ['Customer'])).toBeNull();
  });

  it('never builds a link from an id that is not a real id', () => {
    expect(notificationRoute(link('ServiceRequest', '../../admin'), ['Provider'])).toBeNull();
    expect(notificationRoute(link('Quote', '<script>'), ['Provider'])).toBeNull();
    expect(notificationRoute(link('ServiceRequest', null), ['Customer'])).toBeNull();
  });

  it('has nowhere to go for unknown items or users without a role', () => {
    expect(notificationRoute(link(null, null), ['Customer'])).toBeNull();
    expect(notificationRoute(link('Payment'), ['Customer'])).toBeNull();
    expect(notificationRoute(link('ServiceRequest'), [])).toBeNull();
    expect(notificationRoute(link('ServiceRequest'), ['Admin'])).toBeNull();
  });
});