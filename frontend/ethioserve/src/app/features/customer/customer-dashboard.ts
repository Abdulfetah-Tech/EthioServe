import { DatePipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { LOADING, toLoadState } from '../../core/api/load-state';
import { RequestApi } from '../../core/api/request.api';
import { AuthService } from '../../core/auth/auth.service';
import { CustomerNav } from './customer-nav';
import { statusBadge, statusLabel } from './request-status';

@Component({
  selector: 'app-customer-dashboard',
  imports: [DatePipe, RouterLink, CustomerNav],
  template: `
    <div class="stack">
      <header>
        <h1>Welcome, {{ auth.currentUser()?.displayName }}</h1>
        <p class="muted">Signed in as {{ auth.currentUser()?.email }}</p>
      </header>

      <app-customer-nav />

      <section class="card" aria-labelledby="latest-heading">
        <div class="section-head">
          <h2 id="latest-heading">Your latest requests</h2>
          <a routerLink="/customer/requests/new" class="btn btn-primary">New request</a>
        </div>

        @let latest = recent();
        @if (latest.kind === 'ready') {
          @if (latest.data.items.length === 0) {
            <p class="muted">You have not asked for anything yet. Describe a job and verified providers will see it.</p>
          } @else {
            <ul class="offer-list" role="list">
              @for (item of latest.data.items; track item.id) {
                <li>
                  <span>
                    <a [routerLink]="['/customer/requests', item.id]"><strong>{{ item.title }}</strong></a>
                    <span class="muted"> {{ item.serviceName }} &middot; {{ item.createdAt | date: 'mediumDate' }}</span>
                  </span>
                  <span [class]="badge(item.status)">{{ label(item.status) }}</span>
                </li>
              }
            </ul>
            <p><a routerLink="/customer/requests">See all requests</a></p>
          }
        } @else if (latest.kind === 'error') {
          <p class="alert alert-error" role="alert">{{ latest.message }}</p>
        } @else {
          <p class="muted" role="status">Loading...</p>
        }
      </section>
    </div>
  `,
})
export class CustomerDashboard {
  private readonly requests = inject(RequestApi);

  protected readonly auth = inject(AuthService);
  protected readonly badge = statusBadge;
  protected readonly label = statusLabel;

  protected readonly recent = toSignal(toLoadState(this.requests.list({}, 3)), { initialValue: LOADING });
}