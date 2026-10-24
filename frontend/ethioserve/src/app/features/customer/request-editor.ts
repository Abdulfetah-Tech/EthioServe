import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { catchError, distinctUntilChanged, map, of, startWith, switchMap } from 'rxjs';
import { toApiError } from '../../core/api/api-error';
import { applyServerErrors } from '../../core/api/apply-server-errors';
import { LOADING, LoadState } from '../../core/api/load-state';
import { MarketplaceApi } from '../../core/api/marketplace.api';
import { RequestApi } from '../../core/api/request.api';
import { Category, City, Region, ServiceSummary } from '../../core/models/marketplace.models';
import { RequestDetail } from '../../core/models/request.models';
import {
  MAX_BUDGET,
  buildBody,
  fromApiTime,
  jobRequestRules,
  lengthMessage,
  notInPast,
  todayInAddis,
  trimmedLength,
} from './request-form';

/** Creates a request, or edits one when the route has an id. The service cannot change once a request exists. */
@Component({
  selector: 'app-request-editor',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    @let s = state();
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

        <section class="card">
          <h1>{{ isEdit ? 'Edit your request' : 'New service request' }}</h1>
          <p class="muted">
            Describe the job. Providers who offer this service in your city can see it, but never your address or contact
            details.
          </p>

          @if (errorMessage(); as message) {
            <p class="alert alert-error" role="alert">{{ message }}</p>
          }

          <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
            @if (s.data) {
              <p><strong>Service:</strong> {{ s.data.serviceName }} ({{ s.data.categoryName }})</p>
              <p class="hint">The service cannot be changed. To choose another one, cancel this request and create a new one.</p>
            } @else {
              <div class="grid-2">
                <div class="field">
                  <label for="categoryId">Category</label>
                  <select id="categoryId" formControlName="categoryId" (change)="form.controls.serviceId.setValue('')">
                    <option value="">Choose a category</option>
                    @for (category of categories(); track category.id) {
                      <option [value]="category.id">{{ category.name }}</option>
                    }
                  </select>
                </div>

                <div class="field">
                  <label for="serviceId">Service</label>
                  <select
                    id="serviceId"
                    formControlName="serviceId"
                    [attr.aria-invalid]="form.controls.serviceId.touched && form.controls.serviceId.invalid"
                  >
                    <option value="">Choose a service</option>
                    @for (service of services(); track service.id) {
                      <option [value]="service.id">{{ service.name }}</option>
                    }
                  </select>
                  @if (form.controls.serviceId.touched && form.controls.serviceId.invalid) {
                    <p class="field-error">{{ form.controls.serviceId.errors?.['server'] ?? 'Choose a service.' }}</p>
                  }
                </div>
              </div>
            }

            <div class="field">
              <label for="title">Short title</label>
              <input
                id="title"
                formControlName="title"
                maxlength="150"
                placeholder="For example: Leaking kitchen pipe"
                [attr.aria-invalid]="form.controls.title.touched && form.controls.title.invalid"
              />
              @if (form.controls.title.touched && form.controls.title.invalid) {
                <p class="field-error">{{ lengthMessage(form.controls.title) }}</p>
              }
            </div>

            <div class="field">
              <label for="description">What needs to be done?</label>
              <textarea
                id="description"
                rows="6"
                formControlName="description"
                aria-describedby="description-hint"
                [attr.aria-invalid]="form.controls.description.touched && form.controls.description.invalid"
              ></textarea>
              <p class="hint" id="description-hint">
                At least 20 characters. You have {{ form.controls.description.value.trim().length }}.
              </p>
              @if (form.controls.description.touched && form.controls.description.invalid) {
                <p class="field-error">{{ lengthMessage(form.controls.description) }}</p>
              }
            </div>

            <div class="grid-2">
              <div class="field">
                <label for="regionId">Region</label>
                <select
                  id="regionId"
                  formControlName="regionId"
                  (change)="form.controls.cityId.setValue(0)"
                  [attr.aria-invalid]="form.controls.regionId.touched && form.controls.regionId.invalid"
                >
                  <option [ngValue]="0">Choose a region</option>
                  @for (region of regions(); track region.id) {
                    <option [ngValue]="region.id">{{ region.name }}</option>
                  }
                </select>
                @if (form.controls.regionId.touched && form.controls.regionId.invalid) {
                  <p class="field-error">{{ form.controls.regionId.errors?.['server'] ?? 'Choose a region.' }}</p>
                }
              </div>

              <div class="field">
                <label for="cityId">City</label>
                <select
                  id="cityId"
                  formControlName="cityId"
                  [attr.aria-invalid]="form.controls.cityId.touched && form.controls.cityId.invalid"
                >
                  <option [ngValue]="0">Choose a city</option>
                  @for (city of cities(); track city.id) {
                    <option [ngValue]="city.id">{{ city.name }}</option>
                  }
                </select>
                @if (form.controls.cityId.touched && form.controls.cityId.invalid) {
                  <p class="field-error">{{ form.controls.cityId.errors?.['server'] ?? 'Choose a city.' }}</p>
                }
              </div>
            </div>

            <div class="field">
              <label for="address">Address</label>
              <input
                id="address"
                formControlName="address"
                maxlength="300"
                autocomplete="street-address"
                aria-describedby="address-hint"
                [attr.aria-invalid]="form.controls.address.touched && form.controls.address.invalid"
              />
              <p class="hint" id="address-hint">Private: only shared with the provider you choose, after you accept their quote.</p>
              @if (form.controls.address.touched && form.controls.address.invalid) {
                <p class="field-error">{{ lengthMessage(form.controls.address) }}</p>
              }
            </div>

            <div class="grid-2">
              <div class="field">
                <label for="budgetMin">Budget from (ETB, optional)</label>
                <input
                  id="budgetMin"
                  type="number"
                  min="0"
                  step="0.01"
                  formControlName="budgetMin"
                  [attr.aria-invalid]="form.controls.budgetMin.touched && form.controls.budgetMin.invalid"
                />
                @if (form.controls.budgetMin.touched && form.controls.budgetMin.invalid) {
                  <p class="field-error">{{ form.controls.budgetMin.errors?.['server'] ?? 'Enter an amount of 0 or more.' }}</p>
                }
              </div>

              <div class="field">
                <label for="budgetMax">Budget up to (ETB, optional)</label>
                <input
                  id="budgetMax"
                  type="number"
                  min="0"
                  step="0.01"
                  formControlName="budgetMax"
                  [attr.aria-invalid]="form.controls.budgetMax.touched && (form.controls.budgetMax.invalid || !!form.errors?.['budgetRange'])"
                />
                @if (form.controls.budgetMax.touched && form.controls.budgetMax.invalid) {
                  <p class="field-error">{{ form.controls.budgetMax.errors?.['server'] ?? 'Enter an amount of 0 or more.' }}</p>
                }
                @if (form.errors?.['budgetRange'] && (form.controls.budgetMax.touched || form.controls.budgetMin.touched)) {
                  <p class="field-error">The maximum cannot be less than the minimum.</p>
                }
              </div>
            </div>

            <div class="grid-2">
              <div class="field">
                <label for="preferredDate">Preferred date (optional)</label>
                <input
                  id="preferredDate"
                  type="date"
                  formControlName="preferredDate"
                  [attr.min]="today"
                  [attr.aria-invalid]="form.controls.preferredDate.touched && form.controls.preferredDate.invalid"
                />
                @if (form.controls.preferredDate.touched && form.controls.preferredDate.invalid) {
                  <p class="field-error">
                    {{ form.controls.preferredDate.errors?.['server'] ?? 'The date cannot be in the past.' }}
                  </p>
                }
              </div>

              <div class="field">
                <label for="preferredTime">Preferred time (optional)</label>
                <input id="preferredTime" type="time" formControlName="preferredTime" />
                @if (form.errors?.['timeNeedsDate'] && form.controls.preferredTime.touched) {
                  <p class="field-error">Choose a date as well as a time.</p>
                }
                @if (form.controls.preferredTime.errors?.['server']; as serverMessage) {
                  <p class="field-error">{{ serverMessage }}</p>
                }
              </div>
            </div>

            <div class="form-actions">
              <button type="submit" class="btn btn-primary" [disabled]="submitting()">
                {{ submitting() ? 'Saving...' : isEdit ? 'Save changes' : 'Send request' }}
              </button>
              <a routerLink="/customer/requests" class="btn btn-secondary">Cancel</a>
            </div>
          </form>
        </section>
      </div>
    }
  `,
})
export class RequestEditor {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly requests = inject(RequestApi);
  private readonly market = inject(MarketplaceApi);

  protected readonly requestId = this.route.snapshot.paramMap.get('id');
  protected readonly isEdit = this.requestId !== null;
  protected readonly today = todayInAddis();
  protected readonly lengthMessage = lengthMessage;

  protected readonly state = signal<LoadState<RequestDetail | null>>(
    this.isEdit ? LOADING : { kind: 'ready', data: null },
  );
  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly form = this.fb.nonNullable.group(
    {
      categoryId: [''],
      serviceId: ['', [Validators.required]],
      title: ['', [trimmedLength(5, 150)]],
      description: ['', [trimmedLength(20, 4000)]],
      address: ['', [trimmedLength(3, 300)]],
      regionId: [0, [Validators.min(1)]],
      cityId: [0, [Validators.min(1)]],
      budgetMin: [null as number | null, [Validators.min(0), Validators.max(MAX_BUDGET)]],
      budgetMax: [null as number | null, [Validators.min(0), Validators.max(MAX_BUDGET)]],
      preferredDate: ['', [notInPast(() => todayInAddis())]],
      preferredTime: [''],
    },
    { validators: [jobRequestRules] },
  );

  protected readonly categories = toSignal(this.market.categories().pipe(catchError(() => of<Category[]>([]))), {
    initialValue: [] as Category[],
  });

  protected readonly regions = toSignal(this.market.regions().pipe(catchError(() => of<Region[]>([]))), {
    initialValue: [] as Region[],
  });

  protected readonly services = toSignal(
    this.form.controls.categoryId.valueChanges.pipe(
      startWith(this.form.controls.categoryId.value),
      distinctUntilChanged(),
      switchMap((categoryId) =>
        categoryId
          ? this.market.services({ categoryId, pageSize: 50 }).pipe(
              map((page) => page.items),
              catchError(() => of<ServiceSummary[]>([])),
            )
          : of<ServiceSummary[]>([]),
      ),
    ),
    { initialValue: [] as ServiceSummary[] },
  );

  protected readonly cities = toSignal(
    this.form.controls.regionId.valueChanges.pipe(
      startWith(this.form.controls.regionId.value),
      distinctUntilChanged(),
      switchMap((regionId) =>
        regionId > 0 ? this.market.cities(regionId).pipe(catchError(() => of<City[]>([]))) : of<City[]>([]),
      ),
    ),
    { initialValue: [] as City[] },
  );

  constructor() {
    if (this.requestId !== null) {
      this.form.controls.categoryId.disable();
      this.form.controls.serviceId.disable();

      this.requests.get(this.requestId).subscribe({
        next: (request) => {
          this.form.reset({
            categoryId: request.categoryId,
            serviceId: request.serviceId,
            title: request.title,
            description: request.description,
            address: request.address,
            regionId: request.regionId,
            cityId: request.cityId,
            budgetMin: request.budgetMin,
            budgetMax: request.budgetMax,
            preferredDate: request.preferredDate ?? '',
            preferredTime: fromApiTime(request.preferredTime),
          });
          this.state.set({ kind: 'ready', data: request });
        },
        error: (error: unknown) => {
          const apiError = toApiError(error);
          this.state.set({ kind: 'error', message: apiError.message, httpStatus: apiError.status });
        },
      });
    }
  }

  protected submit(): void {
    this.errorMessage.set(null);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const body = buildBody(value);

    this.submitting.set(true);

    const call =
      this.requestId === null
        ? this.requests.create({ serviceId: value.serviceId, ...body })
        : this.requests.update(this.requestId, body);

    call.subscribe({
      next: (saved) => void this.router.navigate(['/customer/requests', saved.id]),
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