import { Component, computed, inject, input, output, signal } from '@angular/core';
import { Observable } from 'rxjs';
import { toApiError } from '../../core/api/api-error';
import { AppointmentApi } from '../../core/api/appointment.api';
import { ProviderAppointment } from '../../core/models/appointment.models';
import {
  EARLY_START_MINUTES,
  appointmentExplanation,
  appointmentLength,
  appointmentStatusBadge,
  appointmentStatusLabel,
  dateTimeText,
  providerActions,
  slotText,
} from '../../shared/appointment-format';
import { EtbPipe } from '../../shared/etb.pipe';
import { AppointmentScheduler } from './appointment-scheduler';

type Mode = 'idle' | 'reschedule' | 'cancel';

/** One appointment from the provider's side: the details, and only the steps the API will accept. */
@Component({
  selector: 'app-provider-appointment-view',
  imports: [AppointmentScheduler, EtbPipe],
  template: `
    @let a = appointment();
    <div class="stack">
      <p><span [class]="badge(a.status)">{{ label(a.status) }}</span></p>

      <dl class="details">
        <dt>When</dt>
        <dd><strong>{{ slot(a.startAt, a.endAt) }}</strong> ({{ length(a.startAt, a.endAt) }})</dd>
        <dt>Customer</dt>
        <dd>{{ a.customerName }}</dd>
        <dt>Phone</dt>
        <dd><a [href]="'tel:' + a.customerPhone">{{ a.customerPhone }}</a></dd>
        <dt>Address</dt>
        <dd>{{ a.address }}</dd>
        <dt>Agreed price</dt>
        <dd>{{ a.amount | etb }}</dd>
        @if (a.notes) {
          <dt>Your note</dt>
          <dd>{{ a.notes }}</dd>
        }
      </dl>

      <p class="muted">{{ explain(a.status, 'provider') }}</p>

      @if (notice(); as text) {
        <p class="alert alert-success" role="status">{{ text }}</p>
      }
      @if (errorMessage(); as text) {
        <p class="alert alert-error" role="alert">{{ text }}</p>
      }

      @if (mode() === 'reschedule') {
        <app-appointment-scheduler [appointment]="a" (saved)="onRescheduled($event)" (closed)="mode.set('idle')" />
      } @else if (mode() === 'cancel') {
        <div class="row-actions">
          <span>Cancel this appointment? The customer is told, and you can propose a new time afterwards.</span>
          <button type="button" class="btn btn-danger" [disabled]="busy()" (click)="cancel(a)">Yes, cancel it</button>
          <button type="button" class="btn btn-secondary" (click)="mode.set('idle')">Keep it</button>
        </div>
      } @else {
        <div class="form-actions">
          @if (actions().start) {
            <button type="button" class="btn btn-primary" [disabled]="busy()" (click)="start(a)">Start the job</button>
          }
          @if (actions().complete) {
            <button type="button" class="btn btn-primary" [disabled]="busy()" (click)="complete(a)">Mark as complete</button>
          }
          @if (actions().reschedule) {
            <button type="button" class="btn btn-secondary" (click)="mode.set('reschedule')">Change the time</button>
          }
          @if (actions().cancel) {
            <button type="button" class="btn btn-secondary" (click)="mode.set('cancel')">Cancel appointment</button>
          }
        </div>

        @if (actions().startLater) {
          <p class="hint">You can start the job from {{ opensAt() }}.</p>
        }
      }
    </div>
  `,
})
export class ProviderAppointmentView {
  private readonly api = inject(AppointmentApi);

  readonly appointment = input.required<ProviderAppointment>();

  /** Emitted with the new state after every step, so the page can show it. */
  readonly updated = output<ProviderAppointment>();

  protected readonly badge = appointmentStatusBadge;
  protected readonly label = appointmentStatusLabel;
  protected readonly explain = appointmentExplanation;
  protected readonly slot = (start: string, end: string): string => slotText(start, end);
  protected readonly length = appointmentLength;

  protected readonly mode = signal<Mode>('idle');
  protected readonly busy = signal(false);
  protected readonly notice = signal<string | null>(null);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly actions = computed(() => {
    const a = this.appointment();

    return providerActions(a.status, a.startAt);
  });

  protected readonly opensAt = computed(() =>
    dateTimeText(new Date(Date.parse(this.appointment().startAt) - EARLY_START_MINUTES * 60_000).toISOString()),
  );

  protected cancel(a: ProviderAppointment): void {
    this.run(this.api.providerCancel(a.id), 'The appointment was cancelled. You can propose a new time.');
  }

  protected start(a: ProviderAppointment): void {
    this.run(this.api.start(a.id), 'The job has started.');
  }

  protected complete(a: ProviderAppointment): void {
    this.run(this.api.complete(a.id), 'Well done. The job is marked as complete.');
  }

  protected onRescheduled(saved: ProviderAppointment): void {
    this.mode.set('idle');
    this.notice.set('The new time was sent. The customer has to confirm it.');
    this.updated.emit(saved);
  }

  private run(call: Observable<ProviderAppointment>, doneText: string): void {
    this.busy.set(true);
    this.errorMessage.set(null);
    this.notice.set(null);

    call.subscribe({
      next: (saved) => {
        this.mode.set('idle');
        this.busy.set(false);
        this.notice.set(doneText);
        this.updated.emit(saved);
      },
      error: (error: unknown) => {
        this.errorMessage.set(toApiError(error).message);
        this.mode.set('idle');
        this.busy.set(false);
      },
    });
  }
}