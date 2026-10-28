export type NotificationType =
  | 'General'
  | 'NewServiceRequest'
  | 'QuoteReceived'
  | 'QuoteAccepted'
  | 'AppointmentUpdated'
  | 'PaymentCompleted'
  | 'ReviewReceived'
  | 'ProviderVerified';

export interface AppNotification {
  id: string;
  type: NotificationType;
  title: string;
  message: string;
  isRead: boolean;
  relatedEntityType: string | null;
  relatedEntityId: string | null;
  createdAt: string;
}