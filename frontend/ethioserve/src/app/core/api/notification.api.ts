import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { PagedResult } from '../models/marketplace.models';
import { AppNotification } from '../models/notification.models';

export interface NotificationListQuery {
  unreadOnly?: boolean;
  page?: number;
}

/** The signed-in user's own notifications. There is no user id anywhere: the API reads it from the token. */
@Injectable({ providedIn: 'root' })
export class NotificationApi {
  private readonly http = inject(HttpClient);

  list(query: NotificationListQuery, pageSize = 20): Observable<PagedResult<AppNotification>> {
    let params = new HttpParams().set('pageSize', String(pageSize));

    if (query.unreadOnly) {
      params = params.set('unreadOnly', 'true');
    }

    if (query.page && query.page > 1) {
      params = params.set('page', String(query.page));
    }

    return this.http.get<PagedResult<AppNotification>>('/api/notifications', { params });
  }

  unreadCount(): Observable<number> {
    return this.http.get<{ count: number }>('/api/notifications/unread-count').pipe(map((result) => result.count));
  }

  markRead(id: string): Observable<void> {
    return this.http.post<void>(`/api/notifications/${encodeURIComponent(id)}/read`, null);
  }

  markAllRead(): Observable<number> {
    return this.http.post<{ updated: number }>('/api/notifications/read-all', null).pipe(map((result) => result.updated));
  }
}