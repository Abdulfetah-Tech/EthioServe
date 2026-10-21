import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Observable, Subject, catchError, combineLatest, map, of, startWith, switchMap } from 'rxjs';
import { AdminCatalogApi } from '../../core/api/admin-catalog.api';
import { toApiError } from '../../core/api/api-error';
import { applyServerErrors } from '../../core/api/apply-server-errors';
import { LOADING, toLoadState } from '../../core/api/load-state';
import { AdminCategory, AdminService, PricingType } from '../../core/models/admin-catalog.models';
import { EtbPipe } from '../../shared/etb.pipe';
import { Pager } from '../../shared/pager';
import { AdminNav } from './admin-nav';
import {
  ServiceListParams,
  parseServiceParams,
  serviceParamsToQuery,
  withCategoryFilter,
  withInactive,
  withServicePage,
  withServiceSearch,
} from './admin-service-params';
import { MAX_PRICE, PRICING_OPTIONS, needsPrice, normalizePrice, priceProblem, pricingLabel } from './pricing';

const DEFAULT_PARAMS: ServiceListParams = { includeInactive: true };

@Component({
  selector: 'app-admin-services',
  imports: [ReactiveFormsModule, AdminNav, Pager, EtbPipe],
  template: `
    <div class="stack">
      <header>
        <h1>Services</h1>
        <p class="muted">The catalog providers choose from. Deactivated services disappear from public pages.</p>
      </header>

      <app-admin-nav />

      @if (notice(); as text) {
        <p class="alert alert-success" role="status">{{ text }}</p>
      }
      @if (errorMessage(); as text) {
        <p class="alert alert-error" role="alert">{{ text }}</p>
      }

      <section class="card" aria-label="Filters">
        <form class="filters-grid" role="search" (submit)="submitSearch($event, q.value)">
          <div class="field">
            <label for="filter-category">Category</label>
            <select id="filter-category" (change)="onCategory($event)">
              <option value="" [selected]="!params().categoryId">All categories</option>
              @for (category of categories(); track category.id) {
                <option [value]="category.id" [selected]="category.id === params().categoryId">
                  {{ category.name }}{{ category.isActive ? '' : ' (hidden)' }}
                </option>
              }
            </select>
          </div>

          <div class="field">
            <label for="q">Service name</label>
            <input id="q" #q type="search" maxlength="100" [value]="params().search ?? ''" />
          </div>

          <div class="field checkbox-field">
            <input id="inactive" type="checkbox" [checked]="params().includeInactive" (change)="onInactive($event)" />
            <label for="inactive">Show inactive services</label>
          </div>

          <div class="field form-actions">
            <button type="submit" class="btn btn-primary">Search</button>
            <button type="button" class="btn btn-secondary" (click)="openCreate()">New service</button>
          </div>
        </form>
      </section>

      @if (panel(); as target) {
        <section class="card" aria-labelledby="form-heading">
          <h2 id="form-heading">{{ target.service === null ? 'New service' : 'Edit ' + target.service.name }}</h2>

          <form [formGroup]="form" (ngSubmit)="save()" novalidate>
            <div class="grid-2">
              <div class="field">
                <label for="svc-category">Category</label>
                <select
                  id="svc-category"
                  formControlName="categoryId"
                  [attr.aria-invalid]="form.controls.categoryId.touched && form.controls.categoryId.invalid"
                >
                  <option value="">Choose a category</option>
                  @for (category of categories(); track category.id) {
                    <option [value]="category.id">{{ category.name }}{{ category.isActive ? '' : ' (hidden)' }}</option>
                  }
                </select>
                @if (target.service !== null) {
                  <p class="hint">A service stays in its category once created.</p>
                }
                @if (form.controls.categoryId.touched && form.controls.categoryId.invalid) {
                  <p class="field-error">{{ form.controls.categoryId.errors?.['server'] ?? 'Choose a category.' }}</p>
                }
              </div>

              <div class="field">
                <label for="svc-name">Name</label>
                <input
                  id="svc-name"
                  formControlName="name"
                  maxlength="150"
                  [attr.aria-invalid]="form.controls.name.touched && form.controls.name.invalid"
                />
                @if (form.controls.name.touched && form.controls.name.invalid) {
                  <p class="field-error">{{ form.controls.name.errors?.['server'] ?? 'Enter a name of up to 150 characters.' }}</p>
                }
              </div>
            </div>

            <div class="grid-2">
              <div class="field">
                <label for="svc-pricing">Pricing</label>
                <select id="svc-pricing" formControlName="pricingType">
                  @for (option of pricingOptions; track option.value) {
                    <option [value]="option.value">{{ option.label }}</option>
                  }
                </select>
              </div>

              <div class="field">
                <label for="svc-price">Base price (ETB)</label>
                <input
                  id="svc-price"
                  type="number"
                  min="0"
                  step="0.01"
                  formControlName="basePrice"
                  aria-describedby="price-hint"
                  [attr.aria-invalid]="form.controls.basePrice.touched && form.controls.basePrice.invalid"
                />
                <p class="hint" id="price-hint">
                  {{ priceHint() }}
                </p>
                @if (form.controls.basePrice.touched && form.controls.basePrice.invalid) {
                  <p class="field-error">{{ form.controls.basePrice.errors?.['server'] ?? 'Enter a valid price.' }}</p>
                }
              </div>
            </div>

            <div class="field">
              <label for="svc-description">Description (optional)</label>
              <input id="svc-description" formControlName="description" maxlength="1000" />
            </div>

            <div class="form-actions">
              <button type="submit" class="btn btn-primary" [disabled]="busy()">Save service</button>
              <button type="button" class="btn btn-secondary" (click)="panel.set(null)">Cancel</button>
            </div>
          </form>
        </section>
      }

      @let r = results();
      @if (r.kind === 'loading') {
        <p class="muted" role="status">Loading services...</p>
      } @else if (r.kind === 'error') {
        <p class="alert alert-error" role="alert">{{ r.message }}</p>
      } @else {
        <p class="muted" aria-live="polite">
          {{ r.data.totalCount }} {{ r.data.totalCount === 1 ? 'service' : 'services' }} found
        </p>

        @if (r.data.items.length === 0) {
          <div class="card"><p class="muted">No services match these filters.</p></div>
        } @else {
          <div class="card table-wrap">
            <table class="table">
              <caption class="visually-hidden">Services</caption>
              <thead>
                <tr>
                  <th scope="col">Service</th>
                  <th scope="col">Category</th>
                  <th scope="col">Pricing</th>
                  <th scope="col">Status</th>
                  <th scope="col"><span class="visually-hidden">Actions</span></th>
                </tr>
              </thead>
              <tbody>
                @for (service of r.data.items; track service.id) {
                  <tr>
                    <th scope="row">{{ service.name }}</th>
                    <td>{{ service.categoryName }}</td>
                    <td>
                      {{ label(service.pricingType) }}
                      @if (service.basePrice !== null) {
                        <br /><strong>{{ service.basePrice | etb }}</strong>
                      }
                    </td>
                    <td>
                      <span class="badge" [class.badge-ok]="service.isActive">{{ service.isActive ? 'Active' : 'Inactive' }}</span>
                    </td>
                    <td>
                      <div class="row-actions">
                        @if (confirming() === service.id) {
                          <span>Hide {{ service.name }} from customers?</span>
                          <button type="button" class="btn btn-danger" [disabled]="busy()" (click)="setActive(service, false)">
                            Yes, deactivate
                          </button>
                          <button type="button" class="btn btn-secondary" (click)="confirming.set(null)">Keep</button>
                        } @else {
                          <button type="button" class="btn btn-secondary" (click)="openEdit(service)">Edit</button>
                          @if (service.isActive) {
                            <button type="button" class="btn btn-secondary" (click)="confirming.set(service.id)">Deactivate</button>
                          } @else {
                            <button type="button" class="btn btn-secondary" [disabled]="busy()" (click)="setActive(service, true)">
                              Activate
                            </button>
                          }
                        }
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }

        <app-pager [page]="r.data.page" [totalPages]="r.data.totalPages" (pageChange)="goToPage($event)" />
      }
    </div>
  `,
})
export class AdminServices {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(AdminCatalogApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly pageSize = 15;
  protected readonly pricingOptions = PRICING_OPTIONS;
  protected readonly label = pricingLabel;

  protected readonly panel = signal<{ service: AdminService | null } | null>(null); // service null = creating
  protected readonly confirming = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly notice = signal<string | null>(null);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly priceHint = signal('Required for this pricing type. Up to 2 decimals.');

  protected readonly form = this.fb.nonNullable.group({
    categoryId: ['', [Validators.required]],
    name: ['', [Validators.required, Validators.maxLength(150)]],
    description: ['', [Validators.maxLength(1000)]],
    pricingType: ['Fixed' as PricingType],
    basePrice: [null as number | null],
  });

  private readonly refresh$ = new Subject<void>();

  // The address bar is the single source of truth for the list.
  private readonly params$ = this.route.queryParamMap.pipe(map((query) => parseServiceParams(query)));
  protected readonly params = toSignal(this.params$, { initialValue: DEFAULT_PARAMS });

  protected readonly results = toSignal(
    combineLatest([this.params$, this.refresh$.pipe(startWith(undefined))]).pipe(
      switchMap(([p]) => toLoadState(this.api.services(p, this.pageSize))),
    ),
    { initialValue: LOADING },
  );

  protected readonly categories = toSignal(this.api.categories().pipe(catchError(() => of<AdminCategory[]>([]))), {
    initialValue: [] as AdminCategory[],
  });

  constructor() {
    this.form.controls.pricingType.valueChanges.pipe(takeUntilDestroyed()).subscribe((type) => this.syncPrice(type));
    this.syncPrice('Fixed');
  }

  protected openCreate(): void {
    this.clearMessages();
    this.form.controls.categoryId.enable();
    this.form.reset({ categoryId: this.params().categoryId ?? '', name: '', description: '', pricingType: 'Fixed', basePrice: null });
    this.syncPrice('Fixed');
    this.panel.set({ service: null });
  }

  protected openEdit(service: AdminService): void {
    this.clearMessages();
    this.form.controls.categoryId.disable(); // the category is fixed once a service exists
    this.form.reset({
      categoryId: service.categoryId,
      name: service.name,
      description: service.description ?? '',
      pricingType: service.pricingType,
      basePrice: service.basePrice,
    });
    this.syncPrice(service.pricingType);
    this.panel.set({ service });
  }

  protected save(): void {
    const target = this.panel();

    if (!target) {
      return;
    }

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const problem = priceProblem(value.pricingType, value.basePrice);

    if (problem) {
      this.form.controls.basePrice.setErrors({ server: problem });
      this.form.controls.basePrice.markAsTouched();
      return;
    }

    const basePrice = normalizePrice(value.pricingType, value.basePrice);
    const name = value.name.trim();
    const description = value.description.trim() || null;

    this.run(
      target.service === null
        ? this.api.createService({ categoryId: value.categoryId, name, description, basePrice, pricingType: value.pricingType })
        : this.api.updateService(target.service.id, { name, description, basePrice, pricingType: value.pricingType }),
      'Service saved.',
      () => this.panel.set(null),
    );
  }

  protected setActive(service: AdminService, active: boolean): void {
    this.run(
      this.api.setServiceActive(service.id, active),
      active ? `${service.name} is visible again.` : `${service.name} is now hidden from customers.`,
      () => this.confirming.set(null),
    );
  }

  protected submitSearch(event: Event, term: string): void {
    event.preventDefault();
    this.navigate(withServiceSearch(this.params(), term));
  }

  protected onCategory(event: Event): void {
    this.navigate(withCategoryFilter(this.params(), (event.target as HTMLSelectElement).value));
  }

  protected onInactive(event: Event): void {
    this.navigate(withInactive(this.params(), (event.target as HTMLInputElement).checked));
  }

  protected goToPage(page: number): void {
    this.navigate(withServicePage(this.params(), page));
  }

  private run(request: Observable<AdminService>, doneText: string, onDone: () => void): void {
    this.clearMessages();
    this.busy.set(true);

    request.subscribe({
      next: () => {
        onDone();
        this.notice.set(doneText);
        this.busy.set(false);
        this.refresh$.next();
      },
      error: (error: unknown) => this.fail(error),
    });
  }

  private fail(error: unknown): void {
    const apiError = toApiError(error);

    if (apiError.status === 409) {
      this.form.controls.name.setErrors({ server: apiError.message });
      this.form.controls.name.markAsTouched();
    } else {
      const unmatched = applyServerErrors(this.form, apiError);
      const hasFieldErrors = Object.keys(apiError.fieldErrors).length > 0;

      this.errorMessage.set(hasFieldErrors ? (unmatched.join(' ') || null) : apiError.message);
    }

    this.busy.set(false);
  }

  /** A negotiable service has no price field at all; every other type requires one. */
  private syncPrice(type: PricingType): void {
    const control = this.form.controls.basePrice;

    if (needsPrice(type)) {
      control.setValidators([Validators.required, Validators.min(0), Validators.max(MAX_PRICE)]);
      control.enable({ emitEvent: false });
      this.priceHint.set('Required for this pricing type. Up to 2 decimals.');
    } else {
      control.setValue(null, { emitEvent: false });
      control.setValidators([]);
      control.disable({ emitEvent: false });
      this.priceHint.set('Negotiable services have no base price. Customers agree it with the provider.');
    }

    control.updateValueAndValidity({ emitEvent: false });
  }

  private clearMessages(): void {
    this.notice.set(null);
    this.errorMessage.set(null);
    this.confirming.set(null);
  }

  private navigate(next: ServiceListParams): void {
    void this.router.navigate([], { relativeTo: this.route, queryParams: serviceParamsToQuery(next) });
  }
}