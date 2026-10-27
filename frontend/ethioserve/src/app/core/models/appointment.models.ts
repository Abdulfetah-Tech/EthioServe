export type AppointmentStatus = 'Scheduled' | 'Confirmed' | 'InProgress' | 'Completed' | 'Cancelled' | 'NoShow';

export type AppointmentScope = 'Upcoming' | 'History';

/** An appointment as the provider sees it, with the customer's contact details. */
export interface ProviderAppointment {
  id: string;
  requestId: string;
  requestTitle: string;
  serviceName: string;
  cityName: string;
  startAt: string;
  endAt: string;
  address: string;
  notes: string | null;
  status: AppointmentStatus;
  customerName: string;
  customerPhone: string;
  amount: number;
  createdAt: string;
}

/** An appointment as the customer sees it, with the provider's contact details. */
export interface CustomerAppointment {
  id: string;
  requestId: string;
  requestTitle: string;
  serviceName: string;
  startAt: string;
  endAt: string;
  address: string;
  notes: string | null;
  status: AppointmentStatus;
  providerId: string;
  providerName: string;
  providerPhone: string;
  amount: number;
  createdAt: string;
}

/** What a provider sends. The API decides the end time, the status and the owner. */
export interface ScheduleBody {
  startAt: string;
  notes: string | null;
}