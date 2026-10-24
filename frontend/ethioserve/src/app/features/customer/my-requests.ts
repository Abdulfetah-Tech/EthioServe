import { DatePipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { map, switchMap } from 'rxjs';
import { LOADING, toLoadState } from '../../core/api/load-state';
import { RequestApi } from '../../core/api/request.api';
import { Pager } from '../../shared/pager';
import { CustomerNav } from './customer-nav';
import {
  DEFAULT_FILTER,
  ListParams,
  REQUEST_STATUSES,
  StatusFilter,
  listToQuery,
  parseListParams,
  withPage,
  withStatus,
} from './request-list-params';
import { budgetText, statusBadge, statusLabel } from './request-status';

const DEFAULT_PARAMS: ListParams = { status: DEFAULT_FILTER };

@Component({
  selector: 'app-my-requests',
  imports: [DatePipe, RouterLink, CustomerNav, Pager],
  template: `
    <div class="stack">
      <header class="section-head">
        <div>
          <h1>My requests</h1>
          <p class="muted">The jobs you asked for, and where each one stands.</p>
        </div>
        <a routerLink="/customer/requests/new" class="btn btn-primary">New request</a>
      </header>

      <app-customer-nav />

      <section class="card" aria-label="Filters">
        <div class="field">
          <label for="status">Show</label>
          <select id="status" (change)="onStatus($event)">
            @for (option of statusOptions; track option) {
              <option [value]="option" [selected]="option === params().status">
                {{ option === 'All' ? 'All requests' : label(option) }}
              </option>
            }
          </select>
        </div>
      </section>

      @let res = results();
      @if (res.kind === 'loading') {
        <p class="muted" role="status">Loading your requests...</p>
      } @else if (res.kind === 'error') {
        <p class="alert alert-error" role="alert">{{ res.message }}</p>
      } @else {
        <p class="muted" aria-live="polite">
          {{ res.data.totalCount }} {{ res.data.totalCount === 1 ? 'request' : 'requests' }}
        </p>

        @if (res.data.items.length === 0) {
          <div class="card">
            <p><strong>Nothing here yet.</strong></p>
            <p class="muted">Describe a job and verified providers in your city will see it.</p>
            <a routerLink="/customer/requests/new" class="btn btn-primary">Create a request</a>
          </div>
        } @else {
          <div class="card table-wrap">
            <table class="table">
              <caption class="visually-hidden">Your service requests</caption>
              <thead>
                <tr>
                  <th scope="col">Request</th>
                  <th scope="col">Service</th>
                  <th scope="col">Where</th>
                  <th scope="col">Budget</th>
                  <th scope="col">Status</th>
                  <th scope="col">Posted</th>
                </tr>
              </thead>
              <tbody>
                @for (item of res.data.items; track item.id) {
                  <tr>
                    <th scope="row"><a [routerLink]="['/customer/requests', item.id]">{{ item.title }}</a></th>
                    <td>{{ item.serviceName }}</td>
                    <td>{{ item.cityName }}</td>
                    <td>{{ budget(item.budgetMin, item.budgetMax) }}</td>
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
export class MyRequests {
  private readonly api = inject(RequestApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly pageSize = 10;
  protected readonly statusOptions: readonly StatusFilter[] = ['All', ...REQUEST_STATUSES];
  protected readonly label = statusLabel;
  protected readonly badge = statusBadge;
  protected readonly budget = budgetText;

  // The address bar is the single source of truth.
  private readonly params$ = this.route.queryParamMap.pipe(map((query) => parseListParams(query)));
  protected readonly params = toSignal(this.params$, { initialValue: DEFAULT_PARAMS });

  protected readonly results = toSignal(
    this.params$.pipe(
      switchMap((p) =>
        toLoadState(this.api.list({ status: p.status === 'All' ? undefined : p.status, page: p.page }, this.pageSize)),
      ),
    ),
    { initialValue: LOADING },
  );

  protected onStatus(event: Event): void {
    this.navigate(withStatus(this.params(), (event.target as HTMLSelectElement).value));
  }

  protected goToPage(page: number): void {
    this.navigate(withPage(this.params(), page));
  }

  private navigate(next: ListParams): void {
    void this.router.navigate([], { relativeTo: this.route, queryParams: listToQuery(next) });
  }
}