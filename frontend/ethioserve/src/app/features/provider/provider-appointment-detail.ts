import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { distinctUntilChanged, map, switchMap } from 'rxjs';
import { AppointmentApi } from '../../core/api/appointment.api';
import { LOADING, LoadState, toLoadState } from '../../core/api/load-state';
import { ProviderAppointment } from '../../core/models/appointment.models';
import { ProviderAppointmentView } from './provider-appointment-view';
import { ProviderNav } from './provider-nav';

@Component({
  selector: 'app-provider-appointment-detail',
  imports: [RouterLink, ProviderNav, ProviderAppointmentView],
  template: `
    <div class="stack">
      <app-provider-nav />

      <p><a routerLink="/provider/appointments">&larr; Appointments</a></p>

      @let s = detail();
      @if (s.kind === 'loading') {
        <p class="muted" role="status">Loading the appointment...</p>
      } @else if (s.kind === 'error') {
        <section class="card narrow">
          <h1>Appointment not found</h1>
          <p class="alert alert-error" role="alert">{{ s.message }}</p>
          <a routerLink="/provider/appointments" class="btn btn-primary">Back to appointments</a>
        </section>
      } @else {
        <header class="card">
          <h1>{{ s.data.requestTitle }}</h1>
          <p class="muted">{{ s.data.serviceName }} &middot; {{ s.data.cityName }}</p>
        </header>

        <section class="card" aria-labelledby="appointment-heading">
          <h2 id="appointment-heading">Appointment</h2>
          <app-provider-appointment-view [appointment]="s.data" (updated)="onUpdated($event)" />
        </section>
      }
    </div>
  `,
})
export class ProviderAppointmentDetail {
  private readonly api = inject(AppointmentApi);
  private readonly route = inject(ActivatedRoute);

  protected readonly detail = signal<LoadState<ProviderAppointment>>(LOADING);

  constructor() {
    this.route.paramMap
      .pipe(
        map((params) => params.get('id') ?? ''),
        distinctUntilChanged(),
        switchMap((id) => toLoadState(this.api.providerGet(id))),
        takeUntilDestroyed(),
      )
      .subscribe((state) => this.detail.set(state));
  }

  protected onUpdated(appointment: ProviderAppointment): void {
    this.detail.set({ kind: 'ready', data: appointment });
  }
}