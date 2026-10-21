import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Observable } from 'rxjs';
import { AdminCatalogApi } from '../../core/api/admin-catalog.api';
import { toApiError } from '../../core/api/api-error';
import { applyServerErrors } from '../../core/api/apply-server-errors';
import { LOADING, LoadState } from '../../core/api/load-state';
import { AdminCategory } from '../../core/models/admin-catalog.models';
import { AdminNav } from './admin-nav';

@Component({
  selector: 'app-admin-categories',
  imports: [ReactiveFormsModule, AdminNav],
  template: `
    <div class="stack">
      <header>
        <h1>Categories</h1>
        <p class="muted">Group services so customers can browse them. Deactivating a category hides it and its services.</p>
      </header>

      <app-admin-nav />

      @if (notice(); as text) {
        <p class="alert alert-success" role="status">{{ text }}</p>
      }
      @if (errorMessage(); as text) {
        <p class="alert alert-error" role="alert">{{ text }}</p>
      }

      <section class="card" aria-labelledby="list-heading">
        <div class="section-head">
          <h2 id="list-heading">All categories</h2>
          <button type="button" class="btn btn-primary" (click)="openCreate()">New category</button>
        </div>

        @let s = categories();
        @if (s.kind === 'loading') {
          <p class="muted" role="status">Loading categories...</p>
        } @else if (s.kind === 'error') {
          <p class="alert alert-error" role="alert">{{ s.message }}</p>
        } @else {
          <div class="table-wrap">
            <table class="table">
              <caption class="visually-hidden">Service categories</caption>
              <thead>
                <tr>
                  <th scope="col">Name</th>
                  <th scope="col">Description</th>
                  <th scope="col">Active services</th>
                  <th scope="col">Status</th>
                  <th scope="col"><span class="visually-hidden">Actions</span></th>
                </tr>
              </thead>
              <tbody>
                @for (category of s.data; track category.id) {
                  <tr>
                    <th scope="row">{{ category.name }}</th>
                    <td class="muted">{{ category.description }}</td>
                    <td>{{ category.activeServiceCount }} of {{ category.serviceCount }}</td>
                    <td>
                      <span class="badge" [class.badge-ok]="category.isActive">{{ category.isActive ? 'Active' : 'Hidden' }}</span>
                    </td>
                    <td>
                      <div class="row-actions">
                        @if (confirming() === category.id) {
                          <span>Hide {{ category.name }} and its {{ category.activeServiceCount }} active services?</span>
                          <button type="button" class="btn btn-danger" [disabled]="busy()" (click)="setActive(category, false)">
                            Yes, deactivate
                          </button>
                          <button type="button" class="btn btn-secondary" (click)="confirming.set(null)">Keep</button>
                        } @else {
                          <button type="button" class="btn btn-secondary" (click)="openEdit(category)">Edit</button>
                          @if (category.isActive) {
                            <button type="button" class="btn btn-secondary" (click)="confirming.set(category.id)">Deactivate</button>
                          } @else {
                            <button type="button" class="btn btn-secondary" [disabled]="busy()" (click)="setActive(category, true)">
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
      </section>

      @if (panel(); as target) {
        <section class="card" aria-labelledby="form-heading">
          <h2 id="form-heading">{{ target.id === null ? 'New category' : 'Edit category' }}</h2>

          <form [formGroup]="form" (ngSubmit)="save()" novalidate>
            <div class="field">
              <label for="cat-name">Name</label>
              <input
                id="cat-name"
                formControlName="name"
                maxlength="100"
                [attr.aria-invalid]="form.controls.name.touched && form.controls.name.invalid"
              />
              @if (form.controls.name.touched && form.controls.name.invalid) {
                <p class="field-error">{{ form.controls.name.errors?.['server'] ?? 'Enter a name of up to 100 characters.' }}</p>
              }
            </div>

            <div class="field">
              <label for="cat-description">Description (optional)</label>
              <input id="cat-description" formControlName="description" maxlength="500" />
            </div>

            <div class="field">
              <label for="cat-icon">Icon key (optional)</label>
              <input
                id="cat-icon"
                formControlName="iconKey"
                maxlength="300"
                aria-describedby="icon-hint"
                [attr.aria-invalid]="form.controls.iconKey.touched && form.controls.iconKey.invalid"
              />
              <p class="hint" id="icon-hint">Letters, digits, and the characters _ - . / only.</p>
              @if (form.controls.iconKey.touched && form.controls.iconKey.invalid) {
                <p class="field-error">{{ form.controls.iconKey.errors?.['server'] ?? 'Use letters, digits and _ - . / only.' }}</p>
              }
            </div>

            <div class="form-actions">
              <button type="submit" class="btn btn-primary" [disabled]="busy()">Save category</button>
              <button type="button" class="btn btn-secondary" (click)="panel.set(null)">Cancel</button>
            </div>
          </form>
        </section>
      }
    </div>
  `,
})
export class AdminCategories {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(AdminCatalogApi);

  protected readonly categories = signal<LoadState<AdminCategory[]>>(LOADING);
  protected readonly panel = signal<{ id: string | null } | null>(null); // null = closed, id null = creating
  protected readonly confirming = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly notice = signal<string | null>(null);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
    description: ['', [Validators.maxLength(500)]],
    iconKey: ['', [Validators.maxLength(300), Validators.pattern(/^[A-Za-z0-9_\-./]*$/)]],
  });

  constructor() {
    this.reload();
  }

  protected openCreate(): void {
    this.clearMessages();
    this.form.reset({ name: '', description: '', iconKey: '' });
    this.panel.set({ id: null });
  }

  protected openEdit(category: AdminCategory): void {
    this.clearMessages();
    this.form.reset({ name: category.name, description: category.description ?? '', iconKey: category.iconKey ?? '' });
    this.panel.set({ id: category.id });
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
    const request = {
      name: value.name.trim(),
      description: value.description.trim() || null,
      iconKey: value.iconKey.trim() || null,
    };

    this.run(
      target.id === null ? this.api.createCategory(request) : this.api.updateCategory(target.id, request),
      'Category saved.',
      () => this.panel.set(null),
    );
  }

  protected setActive(category: AdminCategory, active: boolean): void {
    this.run(
      this.api.setCategoryActive(category.id, active),
      active ? `${category.name} is visible again.` : `${category.name} is now hidden from customers.`,
      () => this.confirming.set(null),
    );
  }

  private run(request: Observable<AdminCategory>, doneText: string, onDone: () => void): void {
    this.clearMessages();
    this.busy.set(true);

    request.subscribe({
      next: () => {
        onDone();
        this.notice.set(doneText);
        this.busy.set(false);
        this.reload();
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

  private clearMessages(): void {
    this.notice.set(null);
    this.errorMessage.set(null);
    this.confirming.set(null);
  }

  private reload(): void {
    this.api.categories().subscribe({
      next: (data) => this.categories.set({ kind: 'ready', data }),
      error: (error: unknown) => {
        const apiError = toApiError(error);
        this.categories.set({ kind: 'error', message: apiError.message, httpStatus: apiError.status });
      },
    });
  }
}