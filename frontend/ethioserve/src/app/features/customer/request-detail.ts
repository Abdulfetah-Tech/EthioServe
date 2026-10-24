import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { distinctUntilChanged, map, switchMap } from 'rxjs';
import { toApiError } from '../../core/api/api-error';
import { LOADING, LoadState, toLoadState } from '../../core/api/load-state';
import { RequestApi } from '../../core/api/request.api';
import { RequestDetail } from '../../core/models/request.models';
import { budgetText, canCancel, canEdit, nextStepText, statusBadge, statusLabel, whenText } from './request-status';

@Component({
  selector: 'app-request-detail',
  imports: [DatePipe, ReactiveFormsModule, RouterLink],
  template: `
    @let s = detail();
    @if (s.kind === 'loading') {
      <p class="muted" role="status">Loading your request...</p>
    } @else if (s.kind === 'error') {
      <section class="card narrow">
        <h1>Request not found</h1>
        <p class="alert alert-error" role="alert">{{ s.message }}</p>
        <a routerLink="/customer/requests" class="btn btn-primary">Back to my requests</a>
      </section>
    } @else {
      <div class="stack">
        <p><a routerLink="/customer/requests">&larr; My requests</a></p>

        <header class="card">
          <h1>{{ s.data.title }}</h1>
          <p><span [class]="badge(s.data.status)">{{ label(s.data.status) }}</span></p>
          @if (s.data.cancelledReason) {
            <p><strong>Reason for cancelling:</strong> {{ s.data.cancelledReason }}</p>
          }
        </header>

        @if (notice(); as text) {
          <p class="alert alert-success" role="status">{{ text }}</p>
        }
        @if (errorMessage(); as text) {
          <p class="alert alert-error" role="alert">{{ text }}</p>
        }

        <section class="card" aria-labelledby="details-heading">
          <h2 id="details-heading">Details</h2>
          <dl class="details">
            <dt>Service</dt>
            <dd>{{ s.data.serviceName }} ({{ s.data.categoryName }})</dd>
            <dt>Where</dt>
            <dd>{{ s.data.cityName }}, {{ s.data.regionName }}</dd>
            <dt>Address</dt>
            <dd>{{ s.data.address }}</dd>
            <dt>Budget</dt>
            <dd>{{ budget(s.data.budgetMin, s.data.budgetMax) }}</dd>
            <dt>Preferred time</dt>
            <dd>{{ when(s.data.preferredDate, s.data.preferredTime) }}</dd>
            <dt>Posted</dt>
            <dd>{{ s.data.createdAt | date: 'medium' }}</dd>
          </dl>
          <h3>Description</h3>
          <p>{{ s.data.description }}</p>
        </section>

        <section class="card" aria-labelledby="next-heading">
          <h2 id="next-heading">What happens next</h2>
          <p>{{ nextStep(s.data.status) }}</p>
        </section>

        @if (canEdit(s.data.status) || canCancel(s.data.status)) {
          <section class="card" aria-labelledby="actions-heading">
            <h2 id="actions-heading">Manage this request</h2>

            @if (cancelling()) {
              <div class="field">
                <label for="reason">Reason (optional)</label>
                <textarea id="reason" rows="3" maxlength="500" [formControl]="reason"></textarea>
                <p class="hint">Up to 500 characters. Providers do not see this.</p>
              </div>
              <div class="form-actions">
                <button type="button" class="btn btn-danger" [disabled]="busy()" (click)="confirmCancel(s.data)">
                  Yes, cancel this request
                </button>
                <button type="button" class="btn btn-secondary" (click)="cancelling.set(false)">Keep it</button>
              </div>
            } @else {
              <div class="form-actions">
                @if (canEdit(s.data.status)) {
                  <a [routerLink]="['/customer/requests', s.data.id, 'edit']" class="btn btn-secondary">Edit request</a>
                }
                @if (canCancel(s.data.status)) {
                  <button type="button" class="btn btn-secondary" (click)="openCancel()">Cancel request</button>
                }
              </div>
            }
          </section>
        }
      </div>
    }
  `,
})
export class RequestDetailPage {
  private readonly api = inject(RequestApi);
  private readonly route = inject(ActivatedRoute);

  protected readonly label = statusLabel;
  protected readonly badge = statusBadge;
  protected readonly budget = budgetText;
  protected readonly when = whenText;
  protected readonly nextStep = nextStepText;
  protected readonly canEdit = canEdit;
  protected readonly canCancel = canCancel;

  protected readonly detail = signal<LoadState<RequestDetail>>(LOADING);
  protected readonly cancelling = signal(false);
  protected readonly busy = signal(false);
  protected readonly notice = signal<string | null>(null);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly reason = new FormControl('', { nonNullable: true, validators: [Validators.maxLength(500)] });

  constructor() {
    this.route.paramMap
      .pipe(
        map((params) => params.get('id') ?? ''),
        distinctUntilChanged(),
        switchMap((id) => toLoadState(this.api.get(id))),
        takeUntilDestroyed(),
      )
      .subscribe((state) => this.detail.set(state));
  }

  protected openCancel(): void {
    this.reason.reset('');
    this.errorMessage.set(null);
    this.notice.set(null);
    this.cancelling.set(true);
  }

  protected confirmCancel(request: RequestDetail): void {
    if (this.reason.invalid) {
      return;
    }

    this.busy.set(true);
    this.errorMessage.set(null);

    this.api.cancel(request.id, this.reason.value.trim() || null).subscribe({
      next: (updated) => {
        this.detail.set({ kind: 'ready', data: updated });
        this.cancelling.set(false);
        this.notice.set('Your request has been cancelled.');
        this.busy.set(false);
      },
      error: (error: unknown) => {
        this.errorMessage.set(toApiError(error).message);
        this.busy.set(false);
      },
    });
  }
}