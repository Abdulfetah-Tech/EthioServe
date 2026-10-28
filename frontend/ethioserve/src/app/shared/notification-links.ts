import { AppNotification } from '../core/models/notification.models';

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/**
 * Where a notification should take the person, or null when there is no better place than the notification list.
 * The same notification type goes to different pages for customers and providers.
 */
export function notificationRoute(
  notification: Pick<AppNotification, 'relatedEntityType' | 'relatedEntityId'>,
  roles: readonly string[],
): string | null {
  const provider = roles.includes('Provider');
  const customer = roles.includes('Customer');
  const id = notification.relatedEntityId;
  const validId = id !== null && GUID.test(id) ? id : null;

  switch (notification.relatedEntityType) {
    case 'ProviderProfile':
      return provider ? '/provider' : null;
    case 'ServiceRequest':
      if (validId === null) {
        return null;
      }

      return provider ? `/provider/requests/${validId}` : customer ? `/customer/requests/${validId}` : null;
    case 'Quote':
      return provider && validId !== null ? `/provider/quotes/${validId}` : null;
    case 'Appointment':
      if (provider) {
        return validId !== null ? `/provider/appointments/${validId}` : '/provider/appointments';
      }

      return customer ? '/customer/appointments' : null;
    default:
      return null;
  }
}