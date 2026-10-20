import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { catchError, distinctUntilChanged, map, of, startWith, switchMap } from 'rxjs';
import { toApiError } from '../../core/api/api-error';
import { applyServerErrors } from '../../core/api/apply-server-errors';
import { LOADING, LoadState } from '../../core/api/load-state';
import { MarketplaceApi } from '../../core/api/marketplace.api';
import { ProviderApi } from '../../core/api/provider.api';
import { Category, ServiceSummary } from '../../core/models/marketplace.models';
import { MyOffer } from '../../core/models/provider.models';
import { EtbPipe } from '../../shared/etb.pipe';
import { ProviderNav } from './provider-nav';

@Component({
  selector: 'app-provider-services',
  imports: [ReactiveFormsModule, ProviderNav, EtbPipe],
  template: `
    <div class="stack">
      <header>
        <h1>Your services</h1>
        <p class="muted">Customers see these prices on your public page. You can change them at any time.</p>
      </header>

      <app-provider-nav />

      @if (errorMessage(); as message) {
        <p class="alert alert-error" role="alert">{{ message }}</p>
      }

      <section class="card" aria-labelledby="offers-heading">
        <h2 id="offers-heading">Services you offer</h2>

        @let s = offers();
        @if (s.kind === 'loading') {
          <p class="muted" role="status">Loading your services...</p>
        } @else if (s.kind === 'error') {
          <p class="alert alert-error" role="alert">{{ s.message }}</p>
        } @else if (s.data.length === 0) {
          <p class="muted">You have not added any services yet. Add your first one below.</p>
        } @else {
          <div class="table-wrap">
            <table class="table">
              <caption class="visually-hidden">Your services and prices</caption>
              <thead>
                <tr>
                  <th scope="col">Service</th>
                  <th scope="col">Category</th>
                  <th scope="col">Price</th>
                  <th scope="col">Status</th>
                  <th scope="col"><span class="visually-hidden">Actions</span></th>
                </tr>
              </thead>
              <tbody>
                @for (offer of s.data; track offer.id) {
                  <tr>
                    <th scope="row">{{ offer.serviceName }}</th>
                    <td>{{ offer.categoryName }}</td>
                    <td>{{ offer.price | etb }}</td>
                    <td>
                      <span class="badge" [class.badge-ok]="offer.isActive">{{ offer.isActive ? 'Active' : 'Paused' }}</span>
                    </td>
                    <td>
                      <div class="row-actions">
                        @if (confirmingRemoval() === offer.id) {
                          <span>Remove {{ offer.serviceName }}?</span>
                          <button type="button" class="btn btn-danger" [disabled]="busy()" (click)="remove(offer)">Yes, remove</button>
                          <button type="button" class="btn btn-secondary" (click)="confirmingRemoval.set(null)">Keep</button>
                        } @else {
                          <button type="button" class="btn btn-secondary" (click)="startEdit(offer)">Edit</button>
                          <button type="button" class="btn btn-secondary" (click)="confirmingRemoval.set(offer.id)">Remove</button>
                        }
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </section>

      @if (editing(); as offer) {
        <section class="card" aria-labelledby="edit-heading">
          <h2 id="edit-heading">Edit {{ offer.serviceName }}</h2>

          <form [formGroup]="editForm" (ngSubmit)="saveEdit(offer)" novalidate>
            <div class="grid-2">
              <div class="field">
                <label for="edit-price">Price (ETB)</label>
                <input
                  id="edit-price"
                  type="number"
                  min="0"
                  step="0.01"
                  formControlName="price"
                  [attr.aria-invalid]="editForm.controls.price.touched && editForm.controls.price.invalid"
                />
                @if (editForm.controls.price.touched && editForm.controls.price.invalid) {
                  <p class="field-error">{{ editForm.controls.price.errors?.['server'] ?? 'Enter a price of 0 or more.' }}</p>
                }
              </div>

              <div class="field checkbox-field">
                <input id="edit-active" type="checkbox" formControlName="isActive" />
                <label for="edit-active">Show this service to customers</label>
              </div>
            </div>

            <div class="field">
              <label for="edit-description">Note for customers (optional)</label>
              <input id="edit-description" formControlName="description" maxlength="1000" />
              @if (editForm.controls.description.errors?.['server']; as serverMessage) {
                <p class="field-error">{{ serverMessage }}</p>
              }
            </div>

            <div class="form-actions">
              <button type="submit" class="btn btn-primary" [disabled]="busy()">Save changes</button>
              <button type="button" class="btn btn-secondary" (click)="editing.set(null)">Cancel</button>
            </div>
          </form>
        </section>
      }

      <section class="card" aria-labelledby="add-heading">
        <h2 id="add-heading">Add a service</h2>

        <form [formGroup]="addForm" (ngSubmit)="add()" novalidate>
          <div class="grid-2">
            <div class="field">
              <label for="add-category">Category</label>
              <select id="add-category" formControlName="categoryId" (change)="addForm.controls.serviceId.reset('')">
                <option value="">Choose a category</option>
                @for (category of categories(); track category.id) {
                  <option [value]="category.id">{{ category.name }}</option>
                }
              </select>
              @if (addForm.controls.categoryId.touched && addForm.controls.categoryId.invalid) {
                <p class="field-error">Choose a category.</p>
              }
            </div>

            <div class="field">
              <label for="add-service">Service</label>
              <select id="add-service" formControlName="serviceId" [attr.aria-invalid]="addForm.controls.serviceId.touched && addForm.controls.serviceId.invalid">
                <option value="">Choose a service</option>
                @for (service of availableServices(); track service.id) {
                  <option [value]="service.id">{{ service.name }}</option>
                }
              </select>
              @if (addForm.controls.serviceId.touched && addForm.controls.serviceId.invalid) {
                <p class="field-error">{{ addForm.controls.serviceId.errors?.['server'] ?? 'Choose a service.' }}</p>
              }
            </div>
          </div>

          <div class="grid-2">
            <div class="field">
              <label for="add-price">Your price (ETB)</label>
              <input
                id="add-price"
                type="number"
                min="0"
                step="0.01"
                formControlName="price"
                [attr.aria-invalid]="addForm.controls.price.touched && addForm.controls.price.invalid"
              />
              @if (addForm.controls.price.touched && addForm.controls.price.invalid) {
                <p class="field-error">{{ addForm.controls.price.errors?.['server'] ?? 'Enter a price of 0 or more.' }}</p>
              }
            </div>

            <div class="field">
              <label for="add-description">Note for customers (optional)</label>
              <input id="add-description" formControlName="description" maxlength="1000" />
            </div>
          </div>

          <button type="submit" class="btn btn-primary" [disabled]="busy()">Add service</button>
        </form>
      </section>
    </div>
  `,
})
export class ProviderServices {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(ProviderApi);
  private readonly market = inject(MarketplaceApi);

  protected readonly offers = signal<LoadState<MyOffer[]>>(LOADING);
  protected readonly editing = signal<MyOffer | null>(null);
  protected readonly confirmingRemoval = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly addForm = this.fb.nonNullable.group({
    categoryId: ['', [Validators.required]],
    serviceId: ['', [Validators.required]],
    price: [null as number | null, [Validators.required, Validators.min(0), Validators.max(10_000_000)]],
    description: ['', [Validators.maxLength(1000)]],
  });

  protected readonly editForm = this.fb.nonNullable.group({
    price: [null as number | null, [Validators.required, Validators.min(0), Validators.max(10_000_000)]],
    description: ['', [Validators.maxLength(1000)]],
    isActive: [true],
  });

  protected readonly categories = toSignal(this.market.categories().pipe(catchError(() => of<Category[]>([]))), {
    initialValue: [] as Category[],
  });

  private readonly categoryServices = toSignal(
    this.addForm.controls.categoryId.valueChanges.pipe(
      startWith(this.addForm.controls.categoryId.value),
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

  // Services the provider already offers are not offered again.
  protected readonly availableServices = computed(() => {
    const state = this.offers();
    const taken = new Set(state.kind === 'ready' ? state.data.map((offer) => offer.serviceId) : []);

    return this.categoryServices().filter((service) => !taken.has(service.id));
  });

  constructor() {
    this.reload();
  }

  protected add(): void {
    this.errorMessage.set(null);

    if (this.addForm.invalid) {
      this.addForm.markAllAsTouched();
      return;
    }

    const value = this.addForm.getRawValue();
    this.busy.set(true);

    this.api
      .addOffer({ serviceId: value.serviceId, price: Number(value.price), description: value.description.trim() || null })
      .subscribe({
        next: () => {
          this.addForm.reset({ categoryId: value.categoryId, serviceId: '', price: null, description: '' });
          this.busy.set(false);
          this.reload();
        },
        error: (error: unknown) => this.fail(this.addForm, error),
      });
  }

  protected startEdit(offer: MyOffer): void {
    this.confirmingRemoval.set(null);
    this.errorMessage.set(null);
    this.editForm.reset({ price: offer.price, description: offer.description ?? '', isActive: offer.isActive });
    this.editing.set(offer);
  }

  protected saveEdit(offer: MyOffer): void {
    this.errorMessage.set(null);

    if (this.editForm.invalid) {
      this.editForm.markAllAsTouched();
      return;
    }

    const value = this.editForm.getRawValue();
    this.busy.set(true);

    this.api
      .updateOffer(offer.id, {
        price: Number(value.price),
        description: value.description.trim() || null,
        isActive: value.isActive,
      })
      .subscribe({
        next: () => {
          this.editing.set(null);
          this.busy.set(false);
          this.reload();
        },
        error: (error: unknown) => this.fail(this.editForm, error),
      });
  }

  protected remove(offer: MyOffer): void {
    this.errorMessage.set(null);
    this.busy.set(true);

    this.api.removeOffer(offer.id).subscribe({
      next: () => {
        this.confirmingRemoval.set(null);
        this.busy.set(false);
        this.reload();
      },
      error: (error: unknown) => {
        this.errorMessage.set(toApiError(error).message);
        this.busy.set(false);
      },
    });
  }

  private reload(): void {
    this.api.offers().subscribe({
      next: (data) => this.offers.set({ kind: 'ready', data }),
      error: (error: unknown) => {
        const apiError = toApiError(error);
        this.offers.set({ kind: 'error', message: apiError.message, httpStatus: apiError.status });
      },
    });
  }

  private fail(form: Parameters<typeof applyServerErrors>[0], error: unknown): void {
    const apiError = toApiError(error);
    const unmatched = applyServerErrors(form, apiError);
    const hasFieldErrors = Object.keys(apiError.fieldErrors).length > 0;

    this.errorMessage.set(hasFieldErrors ? (unmatched.join(' ') || null) : apiError.message);
    this.busy.set(false);
  }
}