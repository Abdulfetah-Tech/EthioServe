import { Component, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { Subject, combineLatest, forkJoin, map, startWith, switchMap } from 'rxjs';
import { AppointmentApi } from '../../core/api/appointment.api';
import { LOADING, LoadState, toLoadState } from '../../core/api/load-state';
import { ProviderAppointment } from '../../core/models/appointment.models';
import { JobSummary, summarizeJob } from '../../shared/appointment-format';
import { AppointmentScheduler } from './appointment-scheduler';
import { ProviderAppointmentView } from './provider-appointment-view';

/** The provider's side of one accepted job: propose a time, or run the appointment. */
@Component({
  selector: 'app-provider-job',
  imports: [AppointmentScheduler, ProviderAppointmentView],
  template: `
    @let s = state();
    @if (s.kind === 'loading') {
      <p class="muted" role="status">Loading...</p>
    } @else if (s.kind === 'error') {
      <p class="alert alert-error" role="alert">{{ s.message }}</p>
    } @else {
      @if (s.data.current; as current) {
        <app-provider-appointment-view [appointment]="current" (updated)="refresh()" />
      } @else {
        @if (s.data.lastCancelled) {
          <p class="alert alert-error" role="note">The last appointment was cancelled. Propose a new time below.</p>
        }
        <p>
          The customer accepted your quote. Propose a time to do the job. They will be asked to confirm it, and they
          will see your phone number.
        </p>
        <app-appointment-scheduler [requestId]="requestId()" (saved)="refresh()" />
      }
    }
  `,
})
export class ProviderJob {
  private readonly api = inject(AppointmentApi);
  private readonly refresh$ = new Subject<void>();

  readonly requestId = input.required<string>();

  protected readonly state = signal<LoadState<JobSummary<ProviderAppointment>>>(LOADING);

  constructor() {
    combineLatest([toObservable(this.requestId), this.refresh$.pipe(startWith(undefined))])
      .pipe(
        switchMap(([id]) =>
          toLoadState(
            forkJoin({
              upcoming: this.api.providerList({ requestId: id }, 5),
              history: this.api.providerList({ requestId: id, scope: 'History' }, 5),
            }).pipe(map(({ upcoming, history }) => summarizeJob(upcoming.items, history.items))),
          ),
        ),
        takeUntilDestroyed(),
      )
      .subscribe((state) => this.state.set(state));
  }

  protected refresh(): void {
    this.refresh$.next();
  }
}