import { DatePipe } from '@angular/common';
import { Component, inject, input, output, signal } from '@angular/core';
import { toApiError } from '../../core/api/api-error';
import { QuoteApi } from '../../core/api/quote.api';
import { ProviderQuote } from '../../core/models/quote.models';
import { EtbPipe } from '../../shared/etb.pipe';
import { durationText, expiryText, quoteExplanation, quoteStatusBadge, quoteStatusLabel } from '../../shared/quote-format';

/** One of the provider's own quotes: its terms, what happened to it, the customer's details once accepted, and withdrawing. */
@Component({
  selector: 'app-provider-quote-view',
  imports: [DatePipe, EtbPipe],
  template: `
    @let q = quote();
    <div class="stack">
      <p>
        <span [class]="badge(q.status)">{{ label(q.status) }}</span>
        @if (q.status === 'Pending') {
          <span class="muted"> {{ expiry(q.validUntil) }}</span>
        }
      </p>

      <dl class="details">
        <dt>Your price</dt>
        <dd><strong>{{ q.amount | etb }}</strong></dd>
        <dt>Estimated time</dt>
        <dd>{{ duration(q.estimatedDurationMinutes) }}</dd>
        <dt>Valid until</dt>
        <dd>{{ q.validUntil | date: 'medium' }}</dd>
        <dt>Sent</dt>
        <dd>{{ q.createdAt | date: 'medium' }}</dd>
      </dl>

      <p>{{ q.description }}</p>
      <p class="muted">{{ explain(q.status) }}</p>

      @if (q.contact; as contact) {
        <div class="alert alert-success" role="status">
          <p><strong>The customer accepted your quote.</strong> Contact them to arrange the visit.</p>
          <dl class="details">
            <dt>Name</dt>
            <dd>{{ contact.name }}</dd>
            <dt>Phone</dt>
            <dd><a [href]="'tel:' + contact.phoneNumber">{{ contact.phoneNumber }}</a></dd>
            <dt>Address</dt>
            <dd>{{ contact.address }}</dd>
          </dl>
        </div>
      }

      @if (q.status === 'Pending') {
        @if (errorMessage(); as message) {
          <p class="alert alert-error" role="alert">{{ message }}</p>
        }

        @if (confirming()) {
          <div class="row-actions">
            <span>Withdraw this quote?</span>
            <button type="button" class="btn btn-danger" [disabled]="busy()" (click)="withdraw(q.id)">Yes, withdraw</button>
            <button type="button" class="btn btn-secondary" (click)="confirming.set(false)">Keep it</button>
          </div>
        } @else {
          <div class="form-actions">
            <button type="button" class="btn btn-secondary" (click)="confirming.set(true)">Withdraw quote</button>
          </div>
        }
      }
    </div>
  `,
})
export class ProviderQuoteView {
  private readonly api = inject(QuoteApi);

  readonly quote = input.required<ProviderQuote>();
  readonly updated = output<ProviderQuote>();

  protected readonly badge = quoteStatusBadge;
  protected readonly label = quoteStatusLabel;
  protected readonly explain = quoteExplanation;
  protected readonly duration = durationText;
  protected readonly expiry = (validUntil: string): string => expiryText(validUntil);

  protected readonly confirming = signal(false);
  protected readonly busy = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected withdraw(id: string): void {
    this.busy.set(true);
    this.errorMessage.set(null);

    this.api.withdraw(id).subscribe({
      next: (withdrawn) => {
        this.confirming.set(false);
        this.busy.set(false);
        this.updated.emit(withdrawn);
      },
      error: (error: unknown) => {
        this.errorMessage.set(toApiError(error).message);
        this.confirming.set(false);
        this.busy.set(false);
      },
    });
  }
}