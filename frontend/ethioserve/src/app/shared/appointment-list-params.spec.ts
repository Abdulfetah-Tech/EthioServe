import {
  DEFAULT_APPOINTMENT_PARAMS,
  appointmentParamsToQuery,
  parseAppointmentParams,
  withAppointmentPage,
} from './appointment-list-params';

const source = (record: Record<string, string>) => ({ get: (name: string): string | null => record[name] ?? null });

describe('parseAppointmentParams', () => {
  it('shows upcoming appointments by default', () => {
    expect(parseAppointmentParams(source({}))).toEqual({ scope: 'Upcoming' });
    expect(DEFAULT_APPOINTMENT_PARAMS.scope).toBe('Upcoming');
  });

  it('reads the history tab and a page', () => {
    expect(parseAppointmentParams(source({ scope: 'History', page: '3' }))).toEqual({ scope: 'History', page: 3 });
  });

  it('falls back to the defaults for anything unknown or malformed', () => {
    expect(parseAppointmentParams(source({ scope: 'banana', page: '-2' }))).toEqual({ scope: 'Upcoming' });
    expect(parseAppointmentParams(source({ page: '0' })).page).toBeUndefined();
  });
});

describe('appointmentParamsToQuery', () => {
  it('leaves the defaults out of the URL and survives a round trip', () => {
    expect(appointmentParamsToQuery({ scope: 'Upcoming', page: 1 })).toEqual({});

    const params = { scope: 'History' as const, page: 4 };

    expect(parseAppointmentParams(source(appointmentParamsToQuery(params)))).toEqual(params);
  });
});

describe('withAppointmentPage', () => {
  it('changes only the page', () => {
    expect(withAppointmentPage({ scope: 'History' }, 2)).toEqual({ scope: 'History', page: 2 });
  });
});