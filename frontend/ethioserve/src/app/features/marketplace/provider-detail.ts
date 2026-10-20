import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { distinctUntilChanged, map, switchMap } from 'rxjs';
import { LOADING, toLoadState } from '../../core/api/load-state';
import { MarketplaceApi } from '../../core/api/marketplace.api';
import { AuthService } from '../../core/auth/auth.service';
import { PublicOffer, PublicProvider } from '../../core/models/marketplace.models';
import { EtbPipe } from '../../shared/etb.pipe';
import { Rating } from '../../shared/rating';

@Component({
  selector: 'app-provider-detail',
  imports: [RouterLink, Rating, EtbPipe],
  template: `
    @let s = state();
    @if (s.kind === 'loading') {
      <p class="muted" role="status">Loading provider...</p>
    } @else if (s.kind === 'error') {
      <section class="card narrow">
        <h1>Provider not available</h1>
        <p class="alert alert-error" role="alert">{{ errorText(s.httpStatus, s.message) }}</p>
        <a routerLink="/providers" class="btn btn-primary">Back to search</a>
      </section>
    } @else {
      <div class="stack">
        <p><a routerLink="/providers">&larr; All providers</a></p>

        <header class="card">
          <h1>{{ s.data.businessName }}</h1>
          <app-rating [value]="s.data.averageRating" [count]="s.data.totalReviews" />
          <p class="muted">{{ place(s.data) }} &middot; {{ s.data.yearsOfExperience }} years of experience</p>
          @if (s.data.isAvailable) {
            <span class="badge badge-ok">Available</span>
          } @else {
            <span class="badge">Fully booked</span>
          }
        </header>

        @if (s.data.description) {
          <section class="card">
            <h2>About</h2>
            <p>{{ s.data.description }}</p>
          </section>
        }

        <section class="card">
          <h2>Services and prices</h2>
          @for (group of groups(); track group.category) {
            <h3>{{ group.category }}</h3>
            <ul class="offer-list" role="list">
              @for (offer of group.items; track offer.serviceId) {
                <li>
                  <span>
                    <strong>{{ offer.serviceName }}</strong>
                    @if (offer.description) {
                      <span class="muted"> {{ offer.description }}</span>
                    }
                  </span>
                  <span>{{ offer.price | etb }}</span>
                </li>
              }
            </ul>
          } @empty {
            <p class="muted">This provider has not listed any services yet.</p>
          }
        </section>

        <section class="card">
          <h2>Hire {{ s.data.businessName }}</h2>
          <p class="muted">
            Requesting quotes opens in the next release.
            @if (!auth.isAuthenticated()) {
              Create a free account now so you are ready.
            }
          </p>
          @if (!auth.isAuthenticated()) {
            <a routerLink="/register" class="btn btn-primary">Create an account</a>
          }
        </section>
      </div>
    }
  `,
})
export class ProviderDetail {
  private readonly api = inject(MarketplaceApi);
  private readonly route = inject(ActivatedRoute);

  protected readonly auth = inject(AuthService);

  protected readonly state = toSignal(
    this.route.paramMap.pipe(
      map((params) => params.get('id') ?? ''),
      distinctUntilChanged(),
      switchMap((id) => toLoadState(this.api.provider(id))),
    ),
    { initialValue: LOADING },
  );

  protected readonly groups = computed(() => {
    const s = this.state();

    if (s.kind !== 'ready') {
      return [];
    }

    const byCategory = new Map<string, PublicOffer[]>();

    for (const offer of s.data.services) {
      const items = byCategory.get(offer.categoryName) ?? [];
      items.push(offer);
      byCategory.set(offer.categoryName, items);
    }

    return [...byCategory].map(([category, items]) => ({ category, items }));
  });

  protected place(provider: PublicProvider): string {
    const parts = [provider.subCityName, provider.cityName, provider.regionName].filter(
      (part): part is string => !!part,
    );

    return [...new Set(parts)].join(', ');
  }

  protected errorText(httpStatus: number, message: string): string {
    return httpStatus === 404
      ? 'This provider is not available. They may not be verified yet, or have left the marketplace.'
      : message;
  }
}