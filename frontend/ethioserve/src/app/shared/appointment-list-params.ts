import { AppointmentScope } from '../core/models/appointment.models';

export interface ParamSource {
  get(name: string): string | null;
}

export interface AppointmentListParams {
  scope: AppointmentScope;
  page?: number;
}

export const DEFAULT_APPOINTMENT_PARAMS: AppointmentListParams = { scope: 'Upcoming' };

/** Reads the URL. Anything unknown or malformed falls back to the default instead of reaching the API. */
export function parseAppointmentParams(source: ParamSource): AppointmentListParams {
  const pageText = source.get('page');

  return {
    scope: source.get('scope') === 'History' ? 'History' : 'Upcoming',
    page: pageText !== null && /^\d{1,6}$/.test(pageText) && Number(pageText) > 0 ? Number(pageText) : undefined,
  };
}

export function appointmentParamsToQuery(params: AppointmentListParams): Record<string, string> {
  const query: Record<string, string> = {};

  if (params.scope === 'History') {
    query['scope'] = 'History';
  }

  if (params.page && params.page > 1) {
    query['page'] = String(params.page);
  }

  return query;
}

export function withAppointmentPage(params: AppointmentListParams, page: number): AppointmentListParams {
  return { ...params, page };
}