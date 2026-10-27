import { Component, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { toApiError } from '../../core/api/api-error';
import { applyServerErrors } from '../../core/api/apply-server-errors';
import { AppointmentApi } from '../../core/api/appointment.api';
import { ProviderAppointment } from '../../core/models/appointment.models';
import { fromLocalInput, minLocalInput, startRule, timeZoneName, toLocalInput } from '../../shared/appointment-format';

/** Proposes a time for an accepted job, or moves an existing appointment (when `appointment` is given). */
@Component({
  selector: 'app-appointment-scheduler',
  imports: [ReactiveFormsModule],
  template: `
    <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
      @if (errorMessage(); as message) {
        <p class="alert alert-error" role="alert">{{ message }}</p>
      }

      <div class="field">
        <label for="appointment-start">{{ appointment() ? 'New date and time' : 'Date and time' }}</label>
        <input
          id="appointment-start"
          type="datetime-local"
          formControlName="startAt"
          aria-describedby="appointment-hint"
          [attr.min]="minValue"
          [attr.aria-invalid]="form.controls.startAt.touched && form.controls.startAt.invalid"
        />
        <p class="hint" id="appointment-hint">
          Times use your device's time zone ({{ zone }}). The job ends after the time estimate in your quote, and the
          customer has to confirm the time.
        </p>
        @if (form.controls.startAt.touched && form.controls.startAt.invalid) {
          <p class="field-error">{{ startMessage() }}</p>
        }
      </div>

      <div class="field">
        <label for="appointment-notes">Note for the customer (optional)</label>
        <textarea id="appointment-notes" rows="3" maxlength="1000" formControlName="notes"></textarea>
        @if (form.controls.notes.errors?.['server']; as serverMessage) {
          <p class="field-error">{{ serverMessage }}</p>
        }
      </div>

      <div class="form-actions">
        <button type="submit" class="btn btn-primary" [disabled]="submitting()">
          {{ submitting() ? 'Saving...' : appointment() ? 'Save the new time' : 'Propose this time' }}
        </button>
        @if (appointment()) {
          <button type="button" class="btn btn-secondary" (click)="closed.emit()">Keep the current time</button>
        }
      </div>
    </form>
  `,
})
export class AppointmentScheduler implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(AppointmentApi);

  /** The job to book (when proposing a first time). */
  readonly requestId = input<string | null>(null);

  /** The appointment to move (when changing a time). */
  readonly appointment = input<ProviderAppointment | null>(null);

  readonly saved = output<ProviderAppointment>();
  readonly closed = output<void>();

  protected readonly zone = timeZoneName();
  protected readonly minValue = minLocalInput();
  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    startAt: ['', [startRule]],
    notes: ['', [Validators.maxLength(1000)]],
  });

  protected readonly isReschedule = computed(() => this.appointment() !== null);

  ngOnInit(): void {
    const existing = this.appointment();

    if (existing) {
      this.form.reset({ startAt: toLocalInput(existing.startAt), notes: existing.notes ?? '' });
    }
  }

  protected startMessage(): string | null {
    const errors = this.form.controls.startAt.errors;

    return (errors?.['server'] as string | undefined) ?? (errors?.['problem'] as string | undefined) ?? null;
  }

  protected submit(): void {
    this.errorMessage.set(null);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const startAt = fromLocalInput(value.startAt);
    const existing = this.appointment();
    const jobId = this.requestId();

    if (startAt === null || (existing === null && jobId === null)) {
      return;
    }

    const body = { startAt, notes: value.notes.trim() || null };
    const call = existing ? this.api.reschedule(existing.id, body) : this.api.schedule(jobId as string, body);

    this.submitting.set(true);

    call.subscribe({
      next: (saved) => {
        this.submitting.set(false);
        this.saved.emit(saved);
      },
      error: (error: unknown) => {
        const apiError = toApiError(error);
        const unmatched = applyServerErrors(this.form, apiError);
        const hasFieldErrors = Object.keys(apiError.fieldErrors).length > 0;

        // A clash is a 409 whose message names the other appointment, so it is shown as it is.
        this.errorMessage.set(hasFieldErrors ? (unmatched.join(' ') || null) : apiError.message);
        this.submitting.set(false);
      },
    });
  }
}