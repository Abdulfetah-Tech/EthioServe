import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { distinctUntilChanged, map, of, switchMap } from 'rxjs';
import { LOADING, LoadState, toLoadState } from '../../core/api/load-state';
import { MarketplaceApi } from '../../core/api/marketplace.api';
import { Category, City, ProviderSearchParams, Region, ServiceSummary } from '../../core/models/marketplace.models';
import { Pager } from '../../shared/pager';
import { ProviderCard } from './provider-card';
import {
  DEFAULT_SORT,
  FilterKey,
  RATING_OPTIONS,
  SORT_OPTIONS,
  parseSearchParams,
  toQueryParams,
  withFilter,
  withPage,
} from './search-params';

const NO_PARAMS: ProviderSearchParams = {};

function optionsOf<T>(state: LoadState<T[]>): T[] {
  return state.kind === 'ready' ? state.data : [];
}

@Component({
  selector: 'app-providers',
  imports: [ProviderCard, Pager],
  template: `
    <div class="stack">
      <header>
        <h1>Find providers</h1>
        <p class="muted">Verified professionals, with prices shown up front.</p>
      </header>

      <section class="card" aria-label="Search filters">
        <form class="filters-grid" role="search" (submit)="submitSearch($event, q.value)">
          <div class="field">
            <label for="q">Provider name</label>
            <input id="q" #q type="search" maxlength="100" placeholder="For example Abebe" [value]="params().search ?? ''" />
          </div>

          <div class="field">
            <label for="category">Category</label>
            <select id="category" (change)="onFilter('categoryId', $event)">
              <option value="" [selected]="!params().categoryId">All categories</option>
              @for (option of categoryOptions(); track option.id) {
                <option [value]="option.id" [selected]="option.id === params().categoryId">{{ option.name }}</option>
              }
            </select>
          </div>

          <div class="field">
            <label for="service">Service</label>
            <select id="service" [disabled]="!params().categoryId" (change)="onFilter('serviceId', $event)">
              <option value="" [selected]="!params().serviceId">Any service</option>
              @for (option of serviceOptions(); track option.id) {
                <option [value]="option.id" [selected]="option.id === params().serviceId">{{ option.name }}</option>
              }
            </select>
          </div>

          <div class="field">
            <label for="region">Region</label>
            <select id="region" (change)="onFilter('regionId', $event)">
              <option value="" [selected]="!params().regionId">All regions</option>
              @for (option of regionOptions(); track option.id) {
                <option [value]="option.id" [selected]="option.id === params().regionId">{{ option.name }}</option>
              }
            </select>
          </div>

          <div class="field">
            <label for="city">City</label>
            <select id="city" [disabled]="!params().regionId" (change)="onFilter('cityId', $event)">
              <option value="" [selected]="!params().cityId">All cities</option>
              @for (option of cityOptions(); track option.id) {
                <option [value]="option.id" [selected]="option.id === params().cityId">{{ option.name }}</option>
              }
            </select>
          </div>

          <div class="field">
            <label for="rating">Minimum rating</label>
            <select id="rating" (change)="onFilter('minRating', $event)">
              @for (option of ratingOptions; track option.value) {
                <option [value]="option.value" [selected]="option.value === ratingValue()">{{ option.label }}</option>
              }
            </select>
          </div>

          <div class="field">
            <label for="sort">Sort by</label>
            <select id="sort" (change)="onFilter('sort', $event)">
              @for (option of sortOptions; track option.value) {
                <option [value]="option.value" [selected]="option.value === (params().sort ?? defaultSort)">
                  {{ option.label }}
                </option>
              }
            </select>
          </div>

          <div class="field checkbox-field">
            <input id="available" type="checkbox" [checked]="params().availableOnly === true" (change)="onToggle($event)" />
            <label for="available">Available now</label>
          </div>

          <div class="field form-actions">
            <button type="submit" class="btn btn-primary">Search</button>
            @if (hasFilters()) {
              <button type="button" class="btn btn-secondary" (click)="clear()">Clear filters</button>
            }
          </div>
        </form>
      </section>

      <section aria-labelledby="results-heading">
        <h2 id="results-heading" class="visually-hidden">Results</h2>

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
              <p><strong>No providers match these filters.</strong></p>
              <p class="muted">Try removing a filter or choosing a different category or city.</p>
            </div>
          } @else {
            <ul class="provider-grid" role="list">
              @for (provider of r.data.items; track provider.id) {
                <li><app-provider-card [provider]="provider" /></li>
              }
            </ul>
          }

          <app-pager [page]="r.data.page" [totalPages]="r.data.totalPages" (pageChange)="goToPage($event)" />
        }
      </section>
    </div>
  `,
})
export class Providers {
  private readonly api = inject(MarketplaceApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly pageSize = 12;
  protected readonly sortOptions = SORT_OPTIONS;
  protected readonly ratingOptions = RATING_OPTIONS;
  protected readonly defaultSort = DEFAULT_SORT;

  // The address bar is the single source of truth: everything below is derived from it.
  private readonly params$ = this.route.queryParamMap.pipe(map((query) => parseSearchParams(query)));
  protected readonly params = toSignal(this.params$, { initialValue: NO_PARAMS });

  protected readonly results = toSignal(
    this.params$.pipe(switchMap((p) => toLoadState(this.api.searchProviders(p, this.pageSize)))),
    { initialValue: LOADING },
  );

  private readonly categories = toSignal(toLoadState(this.api.categories()), { initialValue: LOADING });
  private readonly regions = toSignal(toLoadState(this.api.regions()), { initialValue: LOADING });

  private readonly services = toSignal(
    this.params$.pipe(
      map((p) => p.categoryId),
      distinctUntilChanged(),
      switchMap((categoryId) =>
        categoryId === undefined
          ? of<LoadState<ServiceSummary[]>>({ kind: 'ready', data: [] })
          : toLoadState(this.api.services({ categoryId, pageSize: 50 }).pipe(map((page) => page.items))),
      ),
    ),
    { initialValue: LOADING },
  );

  private readonly cities = toSignal(
    this.params$.pipe(
      map((p) => p.regionId),
      distinctUntilChanged(),
      switchMap((regionId) =>
        regionId === undefined
          ? of<LoadState<City[]>>({ kind: 'ready', data: [] })
          : toLoadState(this.api.cities(regionId)),
      ),
    ),
    { initialValue: LOADING },
  );

  protected readonly categoryOptions = computed(() => optionsOf<Category>(this.categories()));
  protected readonly regionOptions = computed(() => optionsOf<Region>(this.regions()));
  protected readonly serviceOptions = computed(() => optionsOf<ServiceSummary>(this.services()));
  protected readonly cityOptions = computed(() => optionsOf<City>(this.cities()));

  protected readonly ratingValue = computed(() => {
    const rating = this.params().minRating;

    return rating === undefined ? '' : String(rating);
  });

  protected readonly hasFilters = computed(() => Object.keys(toQueryParams(this.params())).length > 0);

  protected submitSearch(event: Event, term: string): void {
    event.preventDefault();
    this.navigate(withFilter(this.params(), 'search', term.trim()));
  }

  protected onFilter(key: FilterKey, event: Event): void {
    this.navigate(withFilter(this.params(), key, (event.target as HTMLSelectElement).value));
  }

  protected onToggle(event: Event): void {
    this.navigate(withFilter(this.params(), 'availableOnly', (event.target as HTMLInputElement).checked ? 'true' : ''));
  }

  protected goToPage(page: number): void {
    this.navigate(withPage(this.params(), page));
  }

  protected clear(): void {
    this.navigate({});
  }

  private navigate(next: ProviderSearchParams): void {
    void this.router.navigate([], { relativeTo: this.route, queryParams: toQueryParams(next) });
  }
}