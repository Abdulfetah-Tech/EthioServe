import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { PagedResult, ProviderReview } from '../models/marketplace.models';

export interface CreateReviewBody {
  rating: number;
  comment: string | null;
}

@Injectable({ providedIn: 'root' })
export class ReviewApi {
  private readonly http = inject(HttpClient);

  create(requestId: string, body: CreateReviewBody): Observable<ProviderReview> {
    return this.http.post<ProviderReview>(`/api/requests/${encodeURIComponent(requestId)}/reviews`, body);
  }

  list(providerId: string, page = 1, pageSize = 10): Observable<PagedResult<ProviderReview>> {
    const params = new HttpParams().set('page', String(page)).set('pageSize', String(pageSize));
    return this.http.get<PagedResult<ProviderReview>>(
      `/api/providers/${encodeURIComponent(providerId)}/reviews`,
      { params },
    );
  }
}
