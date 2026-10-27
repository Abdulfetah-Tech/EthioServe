import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { map, switchMap } from 'rxjs';
import { AppointmentApi } from '../../core/api/appointment.api';
import { LOADING, toLoadState } from '../../core/api/load-state';
import { appointmentStatusBadge, appointmentStatusLabel, slotText, timeZoneName } from '../../shared/appointment-format';
import {
  AppointmentListParams,
  DEFAULT_APPOINTMENT_PARAMS,
  appointmentParamsToQuery,
  parseAppointmentParams,
  withAppointmentPage,
} from '../../shared/appointment-list-params';
import { Pager } from '../../shared/pager';
import { ProviderNav } from './provider-nav';

@Component({
  selector: 'app-provider-appointments',
  imports: [RouterLink, ProviderNav, Pager],
  template: `
    <div class="stack">
      <header>
        <h1>Appointments</h1>
        <p class="muted">The jobs you have booked. Times use your device's time zone ({{ zone }}).</p>
      </header>

      <app-provider-nav />

      <nav class="tabs" aria-label="Appointment lists">
        <a
          routerLink="/provider/appointments"
          [class.active]="params().scope === 'Upcoming'"
          [attr.aria-current]="params().scope === 'Upcoming' ? 'page' : null"
          >Upcoming</a
        >
        <a
          routerLink="/provider/appointments"
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
            <p><strong>{{ params().scope === 'Upcoming' ? 'Nothing is booked.' : 'No past appointments yet.' }}</strong></p>
            @if (params().scope === 'Upcoming') {
              <p class="muted">When a customer accepts one of your quotes, open it under My quotes and propose a time.</p>
              <a routerLink="/provider/quotes" class="btn btn-primary">Go to my quotes</a>
            }
          </div>
        } @else {
          <ul class="provider-grid" role="list">
            @for (item of res.data.items; track item.id) {
              <li>
                <article class="card provider-card">
                  <h2>
                    <a [routerLink]="['/provider/appointments', item.id]">{{ item.requestTitle }}</a>
                  </h2>
                  <p class="muted">{{ item.serviceName }} &middot; {{ item.cityName }}</p>
                  <p><strong>{{ slot(item.startAt, item.endAt) }}</strong></p>
                  <p class="provider-meta">
                    <span>{{ item.customerName }}</span>
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
export class ProviderAppointments {
  private readonly api = inject(AppointmentApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly zone = timeZoneName();
  protected readonly badge = appointmentStatusBadge;
  protected readonly label = appointmentStatusLabel;
  protected readonly slot = (start: string, end: string): string => slotText(start, end);

  // The address bar is the single source of truth.
  private readonly params$ = this.route.queryParamMap.pipe(map((query) => parseAppointmentParams(query)));
  protected readonly params = toSignal(this.params$, { initialValue: DEFAULT_APPOINTMENT_PARAMS });

  protected readonly results = toSignal(
    this.params$.pipe(switchMap((p) => toLoadState(this.api.providerList({ scope: p.scope, page: p.page })))),
    { initialValue: LOADING },
  );

  protected goToPage(page: number): void {
    const next: AppointmentListParams = withAppointmentPage(this.params(), page);

    void this.router.navigate([], { relativeTo: this.route, queryParams: appointmentParamsToQuery(next) });
  }
}