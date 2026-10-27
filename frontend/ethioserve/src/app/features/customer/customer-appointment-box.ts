import { Component, computed, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { Observable, Subject, combineLatest, forkJoin, map, startWith, switchMap } from 'rxjs';
import { toApiError } from '../../core/api/api-error';
import { AppointmentApi } from '../../core/api/appointment.api';
import { LOADING, LoadState, toLoadState } from '../../core/api/load-state';
import { CustomerAppointment } from '../../core/models/appointment.models';
import { RequestStatus } from '../../core/models/request.models';
import {
  JobSummary,
  appointmentExplanation,
  appointmentLength,
  appointmentStatusBadge,
  appointmentStatusLabel,
  customerActions,
  slotText,
  summarizeJob,
  timeZoneName,
} from '../../shared/appointment-format';
import { EtbPipe } from '../../shared/etb.pipe';

/** The appointment on one of the customer's requests: confirm the proposed time, or cancel it. */
@Component({
  selector: 'app-customer-appointment-box',
  imports: [RouterLink, EtbPipe],
  template: `
    @let s = state();
    @if (s.kind === 'loading') {
      <p class="muted" role="status">Loading...</p>
    } @else if (s.kind === 'error') {
      <p class="alert alert-error" role="alert">{{ s.message }}</p>
    } @else {
      @if (notice(); as text) {
        <p class="alert alert-success" role="status">{{ text }}</p>
      }
      @if (errorMessage(); as text) {
        <p class="alert alert-error" role="alert">{{ text }}</p>
      }

      @if (s.data.current; as a) {
        <div class="stack">
          <p><span [class]="badge(a.status)">{{ label(a.status) }}</span></p>

          @if (actions(a.status).confirm) {
            <p class="alert alert-error" role="note">
              <strong>Please confirm this time</strong>, or cancel the appointment to ask for another.
            </p>
          }

          <dl class="details">
            <dt>When</dt>
            <dd><strong>{{ slot(a.startAt, a.endAt) }}</strong> ({{ length(a.startAt, a.endAt) }})</dd>
            <dt>Provider</dt>
            <dd><a [routerLink]="['/providers', a.providerId]">{{ a.providerName }}</a></dd>
            <dt>Phone</dt>
            <dd><a [href]="'tel:' + a.providerPhone">{{ a.providerPhone }}</a></dd>
            <dt>Address</dt>
            <dd>{{ a.address }}</dd>
            <dt>Agreed price</dt>
            <dd>{{ a.amount | etb }}</dd>
            @if (a.notes) {
              <dt>Note from the provider</dt>
              <dd>{{ a.notes }}</dd>
            }
          </dl>

          <p class="muted">{{ explain(a.status, 'customer') }} Times use your device's time zone ({{ zone }}).</p>

          @if (cancelling()) {
            <div class="row-actions">
              <span>Cancel this appointment? {{ a.providerName }} can then propose a new time.</span>
              <button type="button" class="btn btn-danger" [disabled]="busy()" (click)="cancel(a)">Yes, cancel it</button>
              <button type="button" class="btn btn-secondary" (click)="cancelling.set(false)">Keep it</button>
            </div>
          } @else {
            <div class="form-actions">
              @if (actions(a.status).confirm) {
                <button type="button" class="btn btn-primary" [disabled]="busy()" (click)="confirm(a)">Confirm this time</button>
              }
              @if (actions(a.status).cancel) {
                <button type="button" class="btn btn-secondary" (click)="startCancel()">Cancel appointment</button>
              }
            </div>
          }
        </div>
      } @else if (waitingText(); as text) {
        <p class="muted">{{ text }}</p>
      }
    }
  `,
})
export class CustomerAppointmentBox {
  private readonly api = inject(AppointmentApi);
  private readonly refresh$ = new Subject<void>();

  readonly requestId = input.required<string>();
  readonly requestStatus = input.required<RequestStatus>();

  /** Emitted after a confirm or a cancel, so the page can reload the request itself. */
  readonly changed = output<void>();

  protected readonly zone = timeZoneName();
  protected readonly badge = appointmentStatusBadge;
  protected readonly label = appointmentStatusLabel;
  protected readonly explain = appointmentExplanation;
  protected readonly actions = customerActions;
  protected readonly slot = (start: string, end: string): string => slotText(start, end);
  protected readonly length = appointmentLength;

  protected readonly state = signal<LoadState<JobSummary<CustomerAppointment>>>(LOADING);
  protected readonly cancelling = signal(false);
  protected readonly busy = signal(false);
  protected readonly notice = signal<string | null>(null);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly waitingText = computed(() => {
    const s = this.state();

    if (s.kind !== 'ready') {
      return '';
    }

    if (s.data.lastCancelled) {
      return 'The last appointment was cancelled. The provider can propose a new time, and you will confirm it here.';
    }

    return this.requestStatus() === 'Accepted'
      ? 'Waiting for the provider to propose a time. They can see your preferred date and time. You will confirm the time here.'
      : '';
  });

  constructor() {
    combineLatest([toObservable(this.requestId), this.refresh$.pipe(startWith(undefined))])
      .pipe(
        switchMap(([id]) =>
          toLoadState(
            forkJoin({
              upcoming: this.api.customerList({ requestId: id }, 5),
              history: this.api.customerList({ requestId: id, scope: 'History' }, 5),
            }).pipe(map(({ upcoming, history }) => summarizeJob(upcoming.items, history.items))),
          ),
        ),
        takeUntilDestroyed(),
      )
      .subscribe((state) => this.state.set(state));
  }

  protected startCancel(): void {
    this.notice.set(null);
    this.errorMessage.set(null);
    this.cancelling.set(true);
  }

  protected confirm(a: CustomerAppointment): void {
    this.run(this.api.confirm(a.id), 'You confirmed this time. The provider has been told.');
  }

  protected cancel(a: CustomerAppointment): void {
    this.run(this.api.customerCancel(a.id), 'The appointment was cancelled. The provider can propose a new time.');
  }

  private run(call: Observable<CustomerAppointment>, doneText: string): void {
    this.notice.set(null);
    this.errorMessage.set(null);
    this.busy.set(true);

    call.subscribe({
      next: () => {
        this.finish();
        this.notice.set(doneText);
      },
      error: (error: unknown) => {
        // Show what really happened, then reload so the screen matches the stored state.
        this.finish();
        this.errorMessage.set(toApiError(error).message);
      },
    });
  }

  private finish(): void {
    this.cancelling.set(false);
    this.busy.set(false);
    this.refresh$.next();
    this.changed.emit();
  }
}