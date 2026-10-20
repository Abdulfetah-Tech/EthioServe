import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { LOADING, toLoadState } from '../../core/api/load-state';
import { MarketplaceApi } from '../../core/api/marketplace.api';
import { AuthService } from '../../core/auth/auth.service';
import { homeRouteFor } from '../../core/auth/role-home';
import { ProviderCard } from './provider-card';
import { toQueryParams } from './search-params';

@Component({
  selector: 'app-home',
  imports: [RouterLink, ProviderCard],
  template: `
    <div class="stack">
      <section class="hero">
        <h1>Find trusted professionals. Get the job done.</h1>
        <p class="muted">
          EthioServe connects you with verified plumbers, electricians, cleaners and more. Compare prices, read ratings
          and choose the right professional.
        </p>

        <form class="hero-search" role="search" (submit)="search($event, q.value)">
          <label for="home-search" class="visually-hidden">Search providers by name</label>
          <input id="home-search" #q type="search" placeholder="Search by provider name" maxlength="100" />
          <button type="submit" class="btn btn-primary">Search</button>
        </form>

        <div class="hero-actions">
          <a routerLink="/providers" class="btn btn-secondary">Browse all providers</a>
          @if (auth.isAuthenticated()) {
            <a [routerLink]="home()" class="btn btn-primary">Go to my dashboard</a>
          } @else {
            <a routerLink="/register" class="btn btn-primary">Create an account</a>
          }
        </div>
      </section>

      <section aria-labelledby="categories-heading">
        <h2 id="categories-heading">Browse by category</h2>

        @let cats = categories();
        @if (cats.kind === 'ready') {
          <ul class="tile-grid" role="list">
            @for (category of cats.data; track category.id) {
              <li>
                <a class="tile" routerLink="/providers" [queryParams]="{ categoryId: category.id }">
                  <strong>{{ category.name }}</strong>
                  <span class="muted">{{ category.description }}</span>
                  <span class="muted">{{ category.serviceCount }} services</span>
                </a>
              </li>
            }
          </ul>
        } @else if (cats.kind === 'error') {
          <p class="alert alert-error" role="alert">{{ cats.message }}</p>
        } @else {
          <p class="muted" role="status">Loading categories...</p>
        }
      </section>

      <section aria-labelledby="top-heading">
        <h2 id="top-heading">Top rated providers</h2>

        @let best = top();
        @if (best.kind === 'ready') {
          @if (best.data.items.length === 0) {
            <p class="muted">No providers are listed yet. Please check back soon.</p>
          } @else {
            <ul class="provider-grid" role="list">
              @for (provider of best.data.items; track provider.id) {
                <li><app-provider-card [provider]="provider" /></li>
              }
            </ul>
          }
        } @else if (best.kind === 'error') {
          <p class="alert alert-error" role="alert">{{ best.message }}</p>
        } @else {
          <p class="muted" role="status">Loading providers...</p>
        }
      </section>
    </div>
  `,
})
export class Home {
  private readonly api = inject(MarketplaceApi);
  private readonly router = inject(Router);

  protected readonly auth = inject(AuthService);
  protected readonly home = computed(() => homeRouteFor(this.auth.roles()));

  protected readonly categories = toSignal(toLoadState(this.api.categories()), { initialValue: LOADING });
  protected readonly top = toSignal(toLoadState(this.api.searchProviders({ sort: 'Rating' }, 6)), {
    initialValue: LOADING,
  });

  protected search(event: Event, term: string): void {
    event.preventDefault();

    void this.router.navigate(['/providers'], {
      queryParams: toQueryParams({ search: term.trim() || undefined }),
    });
  }
}