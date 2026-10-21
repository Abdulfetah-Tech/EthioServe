import { DatePipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { map, switchMap } from 'rxjs';
import { AdminApi } from '../../core/api/admin.api';
import { LOADING, toLoadState } from '../../core/api/load-state';
import { Pager } from '../../shared/pager';
import { AdminNav } from './admin-nav';
import {
  DEFAULT_STATUS,
  QueueParams,
  STATUS_FILTERS,
  parseQueueParams,
  queueToQueryParams,
  withPage,
  withSearch,
  withStatus,
} from './admin-provider-params';
import { statusBadgeClass } from './provider-actions';

const DEFAULT_PARAMS: QueueParams = { status: DEFAULT_STATUS };

@Component({
  selector: 'app-admin-providers',
  imports: [DatePipe, RouterLink, AdminNav, Pager],
  template: `
    <div class="stack">
      <header>
        <h1>Provider review</h1>
        <p class="muted">Approve new providers, and manage the ones already listed.</p>
      </header>

      <app-admin-nav />

      <section class="card" aria-label="Filters">
        <form class="filters-grid" role="search" (submit)="submitSearch($event, q.value)">
          <div class="field">
            <label for="status">Status</label>
            <select id="status" (change)="onStatus($event)">
              @for (option of statusOptions; track option) {
                <option [value]="option" [selected]="option === params().status">
                  {{ option === 'All' ? 'All statuses' : option }}
                </option>
              }
            </select>
          </div>

          <div class="field">
            <label for="q">Provider name</label>
            <input id="q" #q type="search" maxlength="100" [value]="params().search ?? ''" />
          </div>

          <div class="field form-actions">
            <button type="submit" class="btn btn-primary">Search</button>
          </div>
        </form>
      </section>

      @let r = results();
      @if (r.kind === 'loading') {
        <p class="muted" role="status">Loading providers...</p>
      } @else if (r.kind === 'error') {
        <p class="alert alert-error" role="alert">{{ r.message }}</p>
      } @else {
        <p class="muted" aria-live="polite">
          {{ r.data.totalCount }} {{ r.data.totalCount === 1 ? 'provider' : 'providers' }} found
        </p>

        @if (r.data.items.length === 0) {
          <div class="card">
            <p><strong>Nobody here.</strong></p>
            <p class="muted">Nothing matches this filter.</p>
          </div>
        } @else {
          <div class="card table-wrap">
            <table class="table">
              <caption class="visually-hidden">Providers</caption>
              <thead>
                <tr>
                  <th scope="col">Provider</th>
                  <th scope="col">City</th>
                  <th scope="col">Status</th>
                  <th scope="col">Services</th>
                  <th scope="col">Registered</th>
                </tr>
              </thead>
              <tbody>
                @for (provider of r.data.items; track provider.id) {
                  <tr>
                    <th scope="row">
                      <a [routerLink]="['/admin/providers', provider.id]">{{ provider.businessName }}</a>
                    </th>
                    <td>{{ provider.cityName }}</td>
                    <td>
                      <span [class]="badge(provider.verificationStatus)">{{ provider.verificationStatus }}</span>
                    </td>
                    <td>{{ provider.activeServiceCount }}</td>
                    <td>{{ provider.createdAt | date: 'mediumDate' }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }

        <app-pager [page]="r.data.page" [totalPages]="r.data.totalPages" (pageChange)="goToPage($event)" />
      }
    </div>
  `,
})
export class AdminProviders {
  private readonly api = inject(AdminApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly pageSize = 15;
  protected readonly statusOptions = STATUS_FILTERS;
  protected readonly badge = statusBadgeClass;

  // The address bar is the single source of truth.
  private readonly params$ = this.route.queryParamMap.pipe(map((query) => parseQueueParams(query)));
  protected readonly params = toSignal(this.params$, { initialValue: DEFAULT_PARAMS });

  protected readonly results = toSignal(
    this.params$.pipe(switchMap((p) => toLoadState(this.api.providers(p, this.pageSize)))),
    { initialValue: LOADING },
  );

  protected onStatus(event: Event): void {
    this.navigate(withStatus(this.params(), (event.target as HTMLSelectElement).value));
  }

  protected submitSearch(event: Event, term: string): void {
    event.preventDefault();
    this.navigate(withSearch(this.params(), term));
  }

  protected goToPage(page: number): void {
    this.navigate(withPage(this.params(), page));
  }

  private navigate(next: QueueParams): void {
    void this.router.navigate([], { relativeTo: this.route, queryParams: queueToQueryParams(next) });
  }
}