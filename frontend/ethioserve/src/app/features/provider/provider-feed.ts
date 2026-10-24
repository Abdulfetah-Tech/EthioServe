import { DatePipe } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { catchError, map, of, switchMap } from 'rxjs';
import { LOADING, toLoadState } from '../../core/api/load-state';
import { ProviderApi } from '../../core/api/provider.api';
import { RequestApi } from '../../core/api/request.api';
import { MyOffer } from '../../core/models/provider.models';
import { Pager } from '../../shared/pager';
import { budgetText, statusBadge, statusLabel, whenText } from '../customer/request-status';
import { FeedParams, feedToQuery, parseFeedParams, withFeedPage, withService } from './feed-params';
import { ProviderNav } from './provider-nav';

const DEFAULT_PARAMS: FeedParams = {};

@Component({
  selector: 'app-provider-feed',
  imports: [DatePipe, RouterLink, ProviderNav, Pager],
  template: `
    <div class="stack">
      <header>
        <h1>Customer requests</h1>
        <p class="muted">Open jobs for the services you offer, in your city. Customers' addresses stay private.</p>
      </header>

      <app-provider-nav />

      <section class="card" aria-label="Filters">
        <div class="field">
          <label for="service">Service</label>
          <select id="service" (change)="onService($event)">
            <option value="" [selected]="!params().serviceId">All my services</option>
            @for (option of serviceOptions(); track option.id) {
              <option [value]="option.id" [selected]="option.id === params().serviceId">{{ option.name }}</option>
            }
          </select>
        </div>
      </section>

      @let res = results();
      @if (res.kind === 'loading') {
        <p class="muted" role="status">Loading requests...</p>
      } @else if (res.kind === 'error') {
        <div class="card">
          <p class="alert alert-error" role="alert">{{ res.message }}</p>
          @if (res.httpStatus === 403) {
            <p>Requests are shown to verified providers only.</p>
            <a routerLink="/provider" class="btn btn-primary">Check my verification status</a>
          }
        </div>
      } @else {
        <p class="muted" aria-live="polite">
          {{ res.data.totalCount }} open {{ res.data.totalCount === 1 ? 'request' : 'requests' }}
        </p>

        @if (res.data.items.length === 0) {
          <div class="card">
            <p><strong>No open requests right now.</strong></p>
            <p class="muted">New requests for your services in your city appear here as customers post them.</p>
          </div>
        } @else {
          <ul class="provider-grid" role="list">
            @for (item of res.data.items; track item.id) {
              <li>
                <article class="card provider-card">
                  <h2>
                    <a [routerLink]="['/provider/requests', item.id]">{{ item.title }}</a>
                  </h2>
                  <p class="muted">{{ item.serviceName }} &middot; {{ item.cityName }}</p>
                  <p>{{ item.summary }}</p>
                  <p class="provider-meta">
                    <strong>{{ budget(item.budgetMin, item.budgetMax) }}</strong>
                    <span [class]="badge(item.status)">{{ label(item.status) }}</span>
                  </p>
                  <p class="muted">
                    {{ when(item.preferredDate, item.preferredTime) }} &middot; posted {{ item.createdAt | date: 'mediumDate' }}
                  </p>
                </article>
              </li>
            }
          </ul>
        }

        <app-pager [page]="res.data.page" [totalPages]="res.data.totalPages" (pageChange)="goToPage($event)" />
      }
    </div>
  `,
})
export class ProviderFeed {
  private readonly api = inject(RequestApi);
  private readonly providerApi = inject(ProviderApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly pageSize = 10;
  protected readonly label = statusLabel;
  protected readonly badge = statusBadge;
  protected readonly budget = budgetText;
  protected readonly when = whenText;

  private readonly params$ = this.route.queryParamMap.pipe(map((query) => parseFeedParams(query)));
  protected readonly params = toSignal(this.params$, { initialValue: DEFAULT_PARAMS });

  protected readonly results = toSignal(
    this.params$.pipe(switchMap((p) => toLoadState(this.api.feed(p, this.pageSize)))),
    { initialValue: LOADING },
  );

  private readonly offers = toSignal(this.providerApi.offers().pipe(catchError(() => of<MyOffer[]>([]))), {
    initialValue: [] as MyOffer[],
  });

  // Only services the provider actively offers can have requests in their feed.
  protected readonly serviceOptions = computed(() =>
    this.offers()
      .filter((offer) => offer.isActive)
      .map((offer) => ({ id: offer.serviceId, name: offer.serviceName }))
      .sort((a, b) => a.name.localeCompare(b.name)),
  );

  protected onService(event: Event): void {
    this.navigate(withService(this.params(), (event.target as HTMLSelectElement).value));
  }

  protected goToPage(page: number): void {
    this.navigate(withFeedPage(this.params(), page));
  }

  private navigate(next: FeedParams): void {
    void this.router.navigate([], { relativeTo: this.route, queryParams: feedToQuery(next) });
  }
}