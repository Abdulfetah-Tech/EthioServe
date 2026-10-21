import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule, ValidatorFn, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Observable, distinctUntilChanged, map, switchMap } from 'rxjs';
import { AdminApi } from '../../core/api/admin.api';
import { toApiError } from '../../core/api/api-error';
import { LOADING, LoadState, toLoadState } from '../../core/api/load-state';
import { AdminProviderDetail } from '../../core/models/admin.models';
import { MyProfile } from '../../core/models/provider.models';
import { EtbPipe } from '../../shared/etb.pipe';
import { AdminAction, ActionInfo, availableActions, statusBadgeClass, waitingText } from './provider-actions';

const notBlank: ValidatorFn = (control) => (String(control.value ?? '').trim().length > 0 ? null : { blank: true });

@Component({
  selector: 'app-admin-provider-page',
  imports: [DatePipe, ReactiveFormsModule, RouterLink, EtbPipe],
  template: `
    <div class="stack">
      <p><a routerLink="/admin/providers">&larr; Back to the review queue</a></p>

      @let s = detail();
      @if (s.kind === 'loading') {
        <p class="muted" role="status">Loading provider...</p>
      } @else if (s.kind === 'error') {
        <section class="card narrow">
          <h1>Provider not found</h1>
          <p class="alert alert-error" role="alert">{{ s.message }}</p>
        </section>
      } @else {
        <header class="card">
          <h1>{{ s.data.profile.businessName }}</h1>
          <p>
            <span [class]="badge(s.data.profile.verificationStatus)">{{ s.data.profile.verificationStatus }}</span>
          </p>
          @if (s.data.profile.verificationNote) {
            <p><strong>Note shown to the provider:</strong> {{ s.data.profile.verificationNote }}</p>
          }
          @if (s.data.verificationReviewedAt) {
            <p class="muted">Last decision: {{ s.data.verificationReviewedAt | date: 'medium' }}</p>
          }
        </header>

        @if (notice(); as text) {
          <p class="alert alert-success" role="status">{{ text }}</p>
        }
        @if (errorMessage(); as text) {
          <p class="alert alert-error" role="alert">{{ text }}</p>
        }

        <section class="card" aria-labelledby="decision-heading">
          <h2 id="decision-heading">Decision</h2>

          @if (actions().length === 0) {
            <p class="muted">{{ waiting(s.data.profile.verificationStatus) }}</p>
          } @else if (choice(); as chosen) {
            <p>{{ chosen.confirmText }}</p>

            @if (chosen.needsReason) {
              <div class="field">
                <label for="reason">Reason (the provider will see this)</label>
                <textarea
                  id="reason"
                  rows="3"
                  maxlength="500"
                  [formControl]="reason"
                  [attr.aria-invalid]="reason.touched && reason.invalid"
                ></textarea>
                @if (reason.touched && reason.invalid) {
                  <p class="field-error">Give a reason of up to 500 characters.</p>
                }
              </div>
            }

            <div class="form-actions">
              <button
                type="button"
                class="btn"
                [class.btn-primary]="chosen.tone === 'primary'"
                [class.btn-danger]="chosen.tone === 'danger'"
                [disabled]="busy()"
                (click)="confirm(s.data, chosen)"
              >
                {{ chosen.label }} provider
              </button>
              <button type="button" class="btn btn-secondary" (click)="choice.set(null)">Cancel</button>
            </div>
          } @else {
            <div class="form-actions">
              @for (info of actions(); track info.action) {
                <button
                  type="button"
                  class="btn"
                  [class.btn-primary]="info.tone === 'primary'"
                  [class.btn-danger]="info.tone === 'danger'"
                  (click)="choose(info)"
                >
                  {{ info.label }}
                </button>
              }
            </div>
          }
        </section>

        <section class="card" aria-labelledby="profile-heading">
          <h2 id="profile-heading">Profile</h2>
          <dl class="details">
            <dt>Phone</dt>
            <dd>{{ s.data.profile.phoneNumber }}</dd>
            <dt>Location</dt>
            <dd>{{ location(s.data.profile) }}</dd>
            <dt>Address</dt>
            <dd>{{ s.data.profile.address || 'Not provided' }}</dd>
            <dt>Experience</dt>
            <dd>{{ s.data.profile.yearsOfExperience }} years</dd>
            <dt>Registered</dt>
            <dd>{{ s.data.createdAt | date: 'medium' }}</dd>
            <dt>Available</dt>
            <dd>{{ s.data.profile.isAvailable ? 'Yes' : 'No' }}</dd>
          </dl>
          <h3>About</h3>
          <p>{{ s.data.profile.description || 'No description yet.' }}</p>
        </section>

        <section class="card" aria-labelledby="offers-heading">
          <h2 id="offers-heading">Services offered</h2>

          @if (s.data.offers.length === 0) {
            <p class="muted">No services yet.</p>
          } @else {
            <div class="table-wrap">
              <table class="table">
                <caption class="visually-hidden">Services offered by this provider</caption>
                <thead>
                  <tr>
                    <th scope="col">Service</th>
                    <th scope="col">Category</th>
                    <th scope="col">Price</th>
                    <th scope="col">Status</th>
                  </tr>
                </thead>
                <tbody>
                  @for (offer of s.data.offers; track offer.id) {
                    <tr>
                      <th scope="row">{{ offer.serviceName }}</th>
                      <td>{{ offer.categoryName }}</td>
                      <td>{{ offer.price | etb }}</td>
                      <td>
                        <span class="badge" [class.badge-ok]="offer.isActive">{{ offer.isActive ? 'Active' : 'Paused' }}</span>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </section>
      }
    </div>
  `,
})
export class AdminProviderPage {
  private readonly api = inject(AdminApi);
  private readonly route = inject(ActivatedRoute);

  protected readonly badge = statusBadgeClass;
  protected readonly waiting = waitingText;

  protected readonly detail = signal<LoadState<AdminProviderDetail>>(LOADING);
  protected readonly choice = signal<ActionInfo | null>(null);
  protected readonly busy = signal(false);
  protected readonly notice = signal<string | null>(null);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly reason = new FormControl('', {
    nonNullable: true,
    validators: [notBlank, Validators.maxLength(500)],
  });

  protected readonly actions = computed(() => {
    const s = this.detail();

    return s.kind === 'ready' ? availableActions(s.data.profile.verificationStatus) : [];
  });

  constructor() {
    this.route.paramMap
      .pipe(
        map((params) => params.get('id') ?? ''),
        distinctUntilChanged(),
        switchMap((id) => toLoadState(this.api.provider(id))),
        takeUntilDestroyed(),
      )
      .subscribe((state) => this.detail.set(state));
  }

  protected choose(info: ActionInfo): void {
    this.reason.reset('');
    this.errorMessage.set(null);
    this.notice.set(null);
    this.choice.set(info);
  }

  protected confirm(detail: AdminProviderDetail, chosen: ActionInfo): void {
    if (chosen.needsReason) {
      this.reason.markAsTouched();

      if (this.reason.invalid) {
        return;
      }
    }

    const id = detail.profile.id;
    const reason = this.reason.value.trim();

    const calls: Record<AdminAction, () => Observable<AdminProviderDetail>> = {
      approve: () => this.api.approve(id),
      reject: () => this.api.reject(id, reason),
      suspend: () => this.api.suspend(id, reason),
      reinstate: () => this.api.reinstate(id),
    };

    this.busy.set(true);
    this.errorMessage.set(null);
    this.notice.set(null);

    calls[chosen.action]().subscribe({
      next: (updated) => {
        this.detail.set({ kind: 'ready', data: updated });
        this.choice.set(null);
        this.notice.set(chosen.doneText);
        this.busy.set(false);
      },
      error: (error: unknown) => {
        this.errorMessage.set(toApiError(error).message);
        this.busy.set(false);
      },
    });
  }

  protected location(profile: MyProfile): string {
    const parts = [profile.subCityName, profile.cityName, profile.regionName].filter((part): part is string => !!part);

    return [...new Set(parts)].join(', ');
  }
}