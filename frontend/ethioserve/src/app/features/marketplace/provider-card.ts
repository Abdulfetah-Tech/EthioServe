import { Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ProviderSummary } from '../../core/models/marketplace.models';
import { EtbPipe } from '../../shared/etb.pipe';
import { Rating } from '../../shared/rating';

@Component({
  selector: 'app-provider-card',
  imports: [RouterLink, Rating, EtbPipe],
  template: `
    @let p = provider();
    <article class="card provider-card">
      <h3>
        <a [routerLink]="['/providers', p.id]">{{ p.businessName }}</a>
      </h3>
      <app-rating [value]="p.averageRating" [count]="p.totalReviews" />
      <p class="muted">{{ place() }} &middot; {{ p.yearsOfExperience }} years of experience</p>
      <p>{{ p.summary }}</p>
      <p class="provider-meta">
        <strong>From {{ p.startingPrice | etb }}</strong>
        @if (p.isAvailable) {
          <span class="badge badge-ok">Available</span>
        } @else {
          <span class="badge">Fully booked</span>
        }
      </p>
    </article>
  `,
})
export class ProviderCard {
  readonly provider = input.required<ProviderSummary>();

  protected readonly place = computed(() => {
    const p = this.provider();

    return p.subCityName ? `${p.subCityName}, ${p.cityName}` : p.cityName;
  });
}