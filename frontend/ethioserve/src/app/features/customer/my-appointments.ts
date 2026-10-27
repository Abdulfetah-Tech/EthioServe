import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { map, switchMap } from 'rxjs';
import { AppointmentApi } from '../../core/api/appointment.api';
import { LOADING, toLoadState } from '../../core/api/load-state';
import { appointmentStatusBadge, appointmentStatusLabel, slotText, timeZoneName } from '../../shared/appointment-format';
import {
  DEFAULT_APPOINTMENT_PARAMS,
  appointmentParamsToQuery,
  parseAppointmentParams,
  withAppointmentPage,
} from '../../shared/appointment-list-params';
import { Pager } from '../../shared/pager';
import { CustomerNav } from './customer-nav';

@Component({
  selector: 'app-my-appointments',
  imports: [RouterLink, CustomerNav, Pager],
  template: `
    <div class="stack">
      <header>
        <h1>Appointments</h1>
        <p class="muted">When your providers are coming. Times use your device's time zone ({{ zone }}).</p>
      </header>

      <app-customer-nav />

      <nav class="tabs" aria-label="Appointment lists">
        <a
          routerLink="/customer/appointments"
          [class.active]="params().scope === 'Upcoming'"
          [attr.aria-current]="params().scope === 'Upcoming' ? 'page' : null"
          >Upcoming</a
        >
        <a
          routerLink="/customer/appointments"
          [queryParams]="{ scope: 'History' }"
          [class.active]="params().scope === 'History'"
          [attr.aria-current]="params().scope === 'History' ? 'page' : null"
          >History</a
        >
      </nav>

      @let res = results();
      @if (res.kind === 'loading') {
        <p class="muted" role="status">Loading appointments...</p>
      } @else if (res.kind === 'error') {
        <p class="alert alert-error" role="alert">{{ res.message }}</p>
      } @else {
        <p class="muted" aria-live="polite">
          {{ res.data.totalCount }} {{ res.data.totalCount === 1 ? 'appointment' : 'appointments' }}
        </p>

        @if (res.data.items.length === 0) {
          <div class="card">
            <p><strong>{{ params().scope === 'Upcoming' ? 'No appointments yet.' : 'No past appointments yet.' }}</strong></p>
            @if (params().scope === 'Upcoming') {
              <p class="muted">When you accept a quote, the provider proposes a time and it appears here.</p>
              <a routerLink="/customer/requests" class="btn btn-primary">Go to my requests</a>
            }
          </div>
        } @else {
          <ul class="provider-grid" role="list">
            @for (item of res.data.items; track item.id) {
              <li>
                <article class="card provider-card">
                  <h2>
                    <a [routerLink]="['/customer/requests', item.requestId]">{{ item.requestTitle }}</a>
                  </h2>
                  <p class="muted">{{ item.serviceName }} &middot; {{ item.providerName }}</p>
                  <p><strong>{{ slot(item.startAt, item.endAt) }}</strong></p>
                  <p class="provider-meta">
                    <a [href]="'tel:' + item.providerPhone">{{ item.providerPhone }}</a>
                    <span [class]="badge(item.status)">{{ label(item.status) }}</span>
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
export class MyAppointments {
  private readonly api = inject(AppointmentApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly zone = timeZoneName();
  protected readonly badge = appointmentStatusBadge;
  protected readonly label = appointmentStatusLabel;
  protected readonly slot = (start: string, end: string): string => slotText(start, end);

  private readonly params$ = this.route.queryParamMap.pipe(map((query) => parseAppointmentParams(query)));
  protected readonly params = toSignal(this.params$, { initialValue: DEFAULT_APPOINTMENT_PARAMS });

  protected readonly results = toSignal(
    this.params$.pipe(switchMap((p) => toLoadState(this.api.customerList({ scope: p.scope, page: p.page })))),
    { initialValue: LOADING },
  );

  protected goToPage(page: number): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: appointmentParamsToQuery(withAppointmentPage(this.params(), page)),
    });
  }
}