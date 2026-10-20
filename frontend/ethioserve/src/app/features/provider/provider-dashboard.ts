import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { toApiError } from '../../core/api/api-error';
import { LOADING, LoadState, toLoadState } from '../../core/api/load-state';
import { ProviderApi } from '../../core/api/provider.api';
import { AuthService } from '../../core/auth/auth.service';
import { MyProfile } from '../../core/models/provider.models';
import { Rating } from '../../shared/rating';
import { ProviderNav } from './provider-nav';
import { STATUS_INFO, canRequestVerification, verificationChecklist } from './verification';

@Component({
  selector: 'app-provider-dashboard',
  imports: [RouterLink, ProviderNav, Rating],
  template: `
    <div class="stack">
      <header>
        <h1>Provider area</h1>
        <p class="muted">Signed in as {{ auth.currentUser()?.displayName }}</p>
      </header>

      <app-provider-nav />

      @let p = profile();
      @if (p.kind === 'loading') {
        <p class="muted" role="status">Loading your profile...</p>
      } @else if (p.kind === 'error') {
        <p class="alert alert-error" role="alert">{{ p.message }}</p>
      } @else {
        @if (message(); as text) {
          <p class="alert alert-error" role="alert">{{ text }}</p>
        }

        <section class="card" aria-labelledby="status-heading">
          <h2 id="status-heading">Verification</h2>
          <p>
            <span class="badge" [class.badge-ok]="p.data.verificationStatus === 'Verified'">
              {{ statusInfo[p.data.verificationStatus].title }}
            </span>
          </p>
          <p>{{ statusInfo[p.data.verificationStatus].text }}</p>

          @if (p.data.verificationNote) {
            <p class="alert alert-error" role="note">
              <strong>Message from our team:</strong> {{ p.data.verificationNote }}
            </p>
          }

          @if (showChecklist()) {
            <ul class="checklist" role="list">
              @for (item of checklist(); track item.key) {
                <li>
                  @if (item.done) {
                    <span aria-hidden="true">&#10003;</span>
                    <span class="visually-hidden">Done:</span>
                    {{ item.label }}
                  } @else {
                    <span aria-hidden="true">&#9675;</span>
                    <span class="visually-hidden">To do:</span>
                    <a [routerLink]="item.link">{{ item.label }}</a>
                  }
                </li>
              }
            </ul>

            <button type="button" class="btn btn-primary" [disabled]="!canRequest() || busy()" (click)="requestVerification()">
              {{ p.data.verificationStatus === 'Rejected' ? 'Request verification again' : 'Request verification' }}
            </button>
          }
        </section>

        <section class="card" aria-labelledby="availability-heading">
          <h2 id="availability-heading">Availability</h2>
          <p class="muted">
            When you are fully booked, customers see a "Fully booked" label next to your name in search.
          </p>
          <p>
            Right now you are marked as <strong>{{ p.data.isAvailable ? 'available' : 'fully booked' }}</strong>.
          </p>
          <button type="button" class="btn btn-secondary" [disabled]="busy()" (click)="toggleAvailability(p.data)">
            {{ p.data.isAvailable ? 'Mark me as fully booked' : 'Mark me as available' }}
          </button>
        </section>

        <section class="card" aria-labelledby="summary-heading">
          <h2 id="summary-heading">Your listing</h2>
          <p><strong>{{ p.data.businessName }}</strong> &middot; {{ p.data.cityName }}</p>
          <p>
            <app-rating [value]="p.data.averageRating" [count]="p.data.totalReviews" />
          </p>
          <p class="muted">{{ p.data.activeServiceCount }} active {{ p.data.activeServiceCount === 1 ? 'service' : 'services' }}</p>
        </section>
      }
    </div>
  `,
})
export class ProviderDashboard {
  private readonly api = inject(ProviderApi);

  protected readonly auth = inject(AuthService);
  protected readonly statusInfo = STATUS_INFO;

  protected readonly profile = signal<LoadState<MyProfile>>(LOADING);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);

  protected readonly checklist = computed(() => {
    const p = this.profile();

    return p.kind === 'ready' ? verificationChecklist(p.data) : [];
  });

  protected readonly showChecklist = computed(() => {
    const p = this.profile();

    return p.kind === 'ready' && (p.data.verificationStatus === 'Unverified' || p.data.verificationStatus === 'Rejected');
  });

  protected readonly canRequest = computed(() => {
    const p = this.profile();

    return p.kind === 'ready' && canRequestVerification(p.data, this.checklist());
  });

  constructor() {
    toLoadState(this.api.profile()).subscribe((state) => this.profile.set(state));
  }

  protected toggleAvailability(profile: MyProfile): void {
    this.run(this.api.setAvailability(!profile.isAvailable));
  }

  protected requestVerification(): void {
    this.run(this.api.requestVerification());
  }

  private run(request: Observable<MyProfile>): void {
    this.busy.set(true);
    this.message.set(null);

    request.subscribe({
      next: (profile) => {
        this.profile.set({ kind: 'ready', data: profile });
        this.busy.set(false);
      },
      error: (error: unknown) => {
        this.message.set(toApiError(error).message);
        this.busy.set(false);
      },
    });
  }
}