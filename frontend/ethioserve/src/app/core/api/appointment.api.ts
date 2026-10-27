import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AppointmentScope, CustomerAppointment, ProviderAppointment, ScheduleBody } from '../models/appointment.models';
import { PagedResult } from '../models/marketplace.models';

export interface AppointmentListQuery {
  scope?: AppointmentScope;
  requestId?: string;
  page?: number;
}

/** Providers book and run their appointments. Customers confirm or cancel the ones on their requests. */
@Injectable({ providedIn: 'root' })
export class AppointmentApi {
  private readonly http = inject(HttpClient);

  schedule(requestId: string, body: ScheduleBody): Observable<ProviderAppointment> {
    return this.http.post<ProviderAppointment>(`/api/providers/me/requests/${encodeURIComponent(requestId)}/appointment`, body);
  }

  providerList(query: AppointmentListQuery, pageSize = 10): Observable<PagedResult<ProviderAppointment>> {
    return this.http.get<PagedResult<ProviderAppointment>>('/api/providers/me/appointments', {
      params: this.params(query, pageSize),
    });
  }

  providerGet(id: string): Observable<ProviderAppointment> {
    return this.http.get<ProviderAppointment>(this.own(id));
  }

  reschedule(id: string, body: ScheduleBody): Observable<ProviderAppointment> {
    return this.http.post<ProviderAppointment>(`${this.own(id)}/reschedule`, body);
  }

  providerCancel(id: string): Observable<ProviderAppointment> {
    return this.http.post<ProviderAppointment>(`${this.own(id)}/cancel`, null);
  }

  start(id: string): Observable<ProviderAppointment> {
    return this.http.post<ProviderAppointment>(`${this.own(id)}/start`, null);
  }

  complete(id: string): Observable<ProviderAppointment> {
    return this.http.post<ProviderAppointment>(`${this.own(id)}/complete`, null);
  }

  customerList(query: AppointmentListQuery, pageSize = 10): Observable<PagedResult<CustomerAppointment>> {
    return this.http.get<PagedResult<CustomerAppointment>>('/api/appointments', {
      params: this.params(query, pageSize),
    });
  }

  confirm(id: string): Observable<CustomerAppointment> {
    return this.http.post<CustomerAppointment>(`${this.theirs(id)}/confirm`, null);
  }

  customerCancel(id: string): Observable<CustomerAppointment> {
    return this.http.post<CustomerAppointment>(`${this.theirs(id)}/cancel`, null);
  }

  /** Only values that differ from the API's defaults are sent. */
  private params(query: AppointmentListQuery, pageSize: number): HttpParams {
    let params = new HttpParams().set('pageSize', String(pageSize));

    if (query.scope === 'History') {
      params = params.set('scope', 'History');
    }

    if (query.requestId) {
      params = params.set('requestId', query.requestId);
    }

    if (query.page && query.page > 1) {
      params = params.set('page', String(query.page));
    }

    return params;
  }

  private own(id: string): string {
    return `/api/providers/me/appointments/${encodeURIComponent(id)}`;
  }

  private theirs(id: string): string {
    return `/api/appointments/${encodeURIComponent(id)}`;
  }
}