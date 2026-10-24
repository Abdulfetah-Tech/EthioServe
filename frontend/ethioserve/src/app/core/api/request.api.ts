import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { PagedResult } from '../models/marketplace.models';
import {
  CreateRequestBody,
  FeedRequestDetail,
  FeedRequestItem,
  JobRequestBody,
  RequestDetail,
  RequestListItem,
  RequestStatus,
} from '../models/request.models';

export interface RequestListQuery {
  status?: RequestStatus;
  page?: number;
}

export interface FeedQuery {
  serviceId?: string;
  page?: number;
}

/** Customer requests and the provider feed. Neither has a user id anywhere: the API reads it from the token. */
@Injectable({ providedIn: 'root' })
export class RequestApi {
  private readonly http = inject(HttpClient);

  create(body: CreateRequestBody): Observable<RequestDetail> {
    return this.http.post<RequestDetail>('/api/requests', body);
  }

  list(query: RequestListQuery, pageSize = 10): Observable<PagedResult<RequestListItem>> {
    let params = new HttpParams().set('pageSize', String(pageSize));

    if (query.status) {
      params = params.set('status', query.status);
    }

    if (query.page && query.page > 1) {
      params = params.set('page', String(query.page));
    }

    return this.http.get<PagedResult<RequestListItem>>('/api/requests', { params });
  }

  get(id: string): Observable<RequestDetail> {
    return this.http.get<RequestDetail>(this.url(id));
  }

  update(id: string, body: JobRequestBody): Observable<RequestDetail> {
    return this.http.put<RequestDetail>(this.url(id), body);
  }

  cancel(id: string, reason: string | null): Observable<RequestDetail> {
    return this.http.post<RequestDetail>(`${this.url(id)}/cancel`, { reason });
  }

  feed(query: FeedQuery, pageSize = 10): Observable<PagedResult<FeedRequestItem>> {
    let params = new HttpParams().set('pageSize', String(pageSize));

    if (query.serviceId) {
      params = params.set('serviceId', query.serviceId);
    }

    if (query.page && query.page > 1) {
      params = params.set('page', String(query.page));
    }

    return this.http.get<PagedResult<FeedRequestItem>>('/api/providers/me/requests', { params });
  }

  feedDetail(id: string): Observable<FeedRequestDetail> {
    return this.http.get<FeedRequestDetail>(`/api/providers/me/requests/${encodeURIComponent(id)}`);
  }

  private url(id: string): string {
    return `/api/requests/${encodeURIComponent(id)}`;
  }
}