import { Component, computed, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { Subject, combineLatest, map, startWith, switchMap } from 'rxjs';
import { toApiError } from '../../core/api/api-error';
import { applyServerErrors } from '../../core/api/apply-server-errors';
import { LOADING, LoadState, toLoadState } from '../../core/api/load-state';
import { QuoteApi } from '../../core/api/quote.api';
import { ProviderQuote } from '../../core/models/quote.models';
import { currentQuote } from '../../shared/quote-format';
import { ProviderQuoteView } from './provider-quote-view';
import {
  DURATION_UNITS,
  DurationUnit,
  VALID_FOR_OPTIONS,
  amountRule,
  buildQuoteBody,
  descriptionRule,
  durationRule,
  problemMessage,
} from './quote-form';

/** On a request in the provider's feed: the quote they already sent, or a form to send one. */
@Component({
  selector: 'app-provider-quote-box',
  imports: [ReactiveFormsModule, ProviderQuoteView],
  template: `
    @let s = quotes();
    @if (s.kind === 'loading') {
      <p class="muted" role="status">Loading...</p>
    } @else if (s.kind === 'error') {
      <p class="alert alert-error" role="alert">{{ s.message }}</p>
    } @else {
      @if (current(); as existing) {
        <app-provider-quote-view [quote]="existing" (updated)="refresh()" />
      }

      @if (canSend()) {
        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <h3>{{ current() ? 'Send a new quote' : 'Send a quote' }}</h3>
          <p class="muted">
            The customer sees your price, your time estimate and your note. Their address and contact details are shared
            only after they accept your quote.
          </p>

          @if (errorMessage(); as message) {
            <p class="alert alert-error" role="alert">{{ message }}</p>
          }

          <div class="field">
            <label for="quote-amount">Your price (ETB)</label>
            <input
              id="quote-amount"
              type="number"
              min="0.01"
              step="0.01"
              formControlName="amount"
              [attr.aria-invalid]="form.controls.amount.touched && form.controls.amount.invalid"
            />
            @if (form.controls.amount.touched && form.controls.amount.invalid) {
              <p class="field-error">{{ problem(form.controls.amount) }}</p>
            }
          </div>

          <div class="grid-2">
            <div class="field">
              <label for="quote-duration">Estimated time</label>
              <input
                id="quote-duration"
                type="number"
                min="1"
                step="any"
                formControlName="durationValue"
                [attr.aria-invalid]="form.controls.durationValue.touched && !!form.errors?.['duration']"
              />
            </div>

            <div class="field">
              <label for="quote-unit">Unit</label>
              <select id="quote-unit" formControlName="durationUnit">
                @for (unit of units; track unit.value) {
                  <option [value]="unit.value">{{ unit.label }}</option>
                }
              </select>
            </div>
          </div>
          @if (form.errors?.['duration'] && form.controls.durationValue.touched) {
            <p class="field-error">{{ form.errors?.['duration'] }}</p>
          }

          <div class="field">
            <label for="quote-valid">The customer can accept this quote for</label>
            <select id="quote-valid" formControlName="validForDays">
              @for (days of validOptions; track days) {
                <option [ngValue]="days">{{ days }} {{ days === 1 ? 'day' : 'days' }}</option>
              }
            </select>
            @if (form.controls.validForDays.errors?.['server']; as serverMessage) {
              <p class="field-error">{{ serverMessage }}</p>
            }
          </div>

          <div class="field">
            <label for="quote-description">What the price includes</label>
            <textarea
              id="quote-description"
              rows="4"
              formControlName="description"
              aria-describedby="quote-description-hint"
              [attr.aria-invalid]="form.controls.description.touched && form.controls.description.invalid"
            ></textarea>
            <p class="hint" id="quote-description-hint">
              At least 10 characters, for example parts, labour and any visit charge. You have
              {{ form.controls.description.value.trim().length }}.
            </p>
            @if (form.controls.description.touched && form.controls.description.invalid) {
              <p class="field-error">{{ problem(form.controls.description) }}</p>
            }
          </div>

          <button type="submit" class="btn btn-primary" [disabled]="submitting()">
            {{ submitting() ? 'Sending...' : 'Send quote' }}
          </button>
        </form>
      }
    }
  `,
})
export class ProviderQuoteBox {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(QuoteApi);
  private readonly refresh$ = new Subject<void>();

  readonly requestId = input.required<string>();

  protected readonly units = DURATION_UNITS;
  protected readonly validOptions = VALID_FOR_OPTIONS;
  protected readonly problem = problemMessage;

  protected readonly quotes = signal<LoadState<ProviderQuote[]>>(LOADING);
  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly form = this.fb.nonNullable.group(
    {
      amount: [null as number | null, [amountRule]],
      description: ['', [descriptionRule]],
      durationValue: [null as number | null],
      durationUnit: ['hours' as DurationUnit],
      validForDays: [7],
    },
    { validators: [durationRule] },
  );

  protected readonly current = computed(() => {
    const s = this.quotes();

    return s.kind === 'ready' ? currentQuote(s.data) : null;
  });

  // A new quote can be sent unless one is open or already accepted.
  protected readonly canSend = computed(() => {
    const s = this.quotes();

    if (s.kind !== 'ready') {
      return false;
    }

    const existing = currentQuote(s.data);

    return existing === null || (existing.status !== 'Pending' && existing.status !== 'Accepted');
  });

  constructor() {
    combineLatest([toObservable(this.requestId), this.refresh$.pipe(startWith(undefined))])
      .pipe(
        switchMap(([id]) => toLoadState(this.api.mine({ requestId: id }, 20).pipe(map((page) => page.items)))),
        takeUntilDestroyed(),
      )
      .subscribe((state) => this.quotes.set(state));
  }

  protected refresh(): void {
    this.refresh$.next();
  }

  protected submit(): void {
    this.errorMessage.set(null);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);

    this.api.submit(this.requestId(), buildQuoteBody(this.form.getRawValue())).subscribe({
      next: () => {
        this.form.reset({ amount: null, description: '', durationValue: null, durationUnit: 'hours', validForDays: 7 });
        this.submitting.set(false);
        this.refresh$.next();
      },
      error: (error: unknown) => {
        const apiError = toApiError(error);
        const unmatched = applyServerErrors(this.form, apiError);
        const hasFieldErrors = Object.keys(apiError.fieldErrors).length > 0;

        this.errorMessage.set(hasFieldErrors ? (unmatched.join(' ') || null) : apiError.message);
        this.submitting.set(false);
      },
    });
  }
}