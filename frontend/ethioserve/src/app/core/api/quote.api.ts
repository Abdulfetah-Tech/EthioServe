import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { PagedResult } from '../models/marketplace.models';
import { CustomerQuote, ProviderQuote, SubmitQuoteBody } from '../models/quote.models';

export interface MyQuotesQuery {
  requestId?: string;
  page?: number;
}

/** Providers send and withdraw their own quotes. Customers compare, accept and decline the ones on their requests. */
@Injectable({ providedIn: 'root' })
export class QuoteApi {
  private readonly http = inject(HttpClient);

  submit(requestId: string, body: SubmitQuoteBody): Observable<ProviderQuote> {
    return this.http.post<ProviderQuote>(`/api/providers/me/requests/${encodeURIComponent(requestId)}/quotes`, body);
  }

  mine(query: MyQuotesQuery, pageSize = 10): Observable<PagedResult<ProviderQuote>> {
    let params = new HttpParams().set('pageSize', String(pageSize));

    if (query.requestId) {
      params = params.set('requestId', query.requestId);
    }

    if (query.page && query.page > 1) {
      params = params.set('page', String(query.page));
    }

    return this.http.get<PagedResult<ProviderQuote>>('/api/providers/me/quotes', { params });
  }

  get(id: string): Observable<ProviderQuote> {
    return this.http.get<ProviderQuote>(this.own(id));
  }

  withdraw(id: string): Observable<ProviderQuote> {
    return this.http.post<ProviderQuote>(`${this.own(id)}/withdraw`, null);
  }

  forRequest(requestId: string): Observable<CustomerQuote[]> {
    return this.http.get<CustomerQuote[]>(this.onRequest(requestId));
  }

  accept(requestId: string, quoteId: string): Observable<CustomerQuote> {
    return this.http.post<CustomerQuote>(`${this.onRequest(requestId)}/${encodeURIComponent(quoteId)}/accept`, null);
  }

  decline(requestId: string, quoteId: string): Observable<CustomerQuote> {
    return this.http.post<CustomerQuote>(`${this.onRequest(requestId)}/${encodeURIComponent(quoteId)}/decline`, null);
  }

  private own(id: string): string {
    return `/api/providers/me/quotes/${encodeURIComponent(id)}`;
  }

  private onRequest(requestId: string): string {
    return `/api/requests/${encodeURIComponent(requestId)}/quotes`;
  }
}