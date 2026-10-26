import { DatePipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { map, switchMap } from 'rxjs';
import { LOADING, toLoadState } from '../../core/api/load-state';
import { QuoteApi } from '../../core/api/quote.api';
import { EtbPipe } from '../../shared/etb.pipe';
import { Pager } from '../../shared/pager';
import { quoteStatusBadge, quoteStatusLabel } from '../../shared/quote-format';
import { feedToQuery, parseFeedParams, withFeedPage } from './feed-params';
import { ProviderNav } from './provider-nav';

@Component({
  selector: 'app-my-quotes',
  imports: [DatePipe, RouterLink, ProviderNav, Pager, EtbPipe],
  template: `
    <div class="stack">
      <header>
        <h1>My quotes</h1>
        <p class="muted">Every quote you sent, and what happened to it.</p>
      </header>

      <app-provider-nav />

      @let res = results();
      @if (res.kind === 'loading') {
        <p class="muted" role="status">Loading your quotes...</p>
      } @else if (res.kind === 'error') {
        <p class="alert alert-error" role="alert">{{ res.message }}</p>
      } @else {
        <p class="muted" aria-live="polite">
          {{ res.data.totalCount }} {{ res.data.totalCount === 1 ? 'quote' : 'quotes' }}
        </p>

        @if (res.data.items.length === 0) {
          <div class="card">
            <p><strong>You have not sent any quotes yet.</strong></p>
            <p class="muted">Open a customer request, check the details, and send your price.</p>
            <a routerLink="/provider/requests" class="btn btn-primary">Browse requests</a>
          </div>
        } @else {
          <div class="card table-wrap">
            <table class="table">
              <caption class="visually-hidden">Your quotes</caption>
              <thead>
                <tr>
                  <th scope="col">Request</th>
                  <th scope="col">Service</th>
                  <th scope="col">City</th>
                  <th scope="col">Price</th>
                  <th scope="col">Status</th>
                  <th scope="col">Sent</th>
                </tr>
              </thead>
              <tbody>
                @for (item of res.data.items; track item.id) {
                  <tr>
                    <th scope="row"><a [routerLink]="['/provider/quotes', item.id]">{{ item.requestTitle }}</a></th>
                    <td>{{ item.serviceName }}</td>
                    <td>{{ item.cityName }}</td>
                    <td>{{ item.amount | etb }}</td>
                    <td><span [class]="badge(item.status)">{{ label(item.status) }}</span></td>
                    <td>{{ item.createdAt | date: 'mediumDate' }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }

        <app-pager [page]="res.data.page" [totalPages]="res.data.totalPages" (pageChange)="goToPage($event)" />
      }
    </div>
  `,
})
export class MyQuotes {
  private readonly api = inject(QuoteApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly badge = quoteStatusBadge;
  protected readonly label = quoteStatusLabel;

  protected readonly results = toSignal(
    this.route.queryParamMap.pipe(
      map((query) => parseFeedParams(query)),
      switchMap((p) => toLoadState(this.api.mine({ page: p.page }))),
    ),
    { initialValue: LOADING },
  );

  protected goToPage(page: number): void {
    void this.router.navigate([], { relativeTo: this.route, queryParams: feedToQuery(withFeedPage({}, page)) });
  }
}