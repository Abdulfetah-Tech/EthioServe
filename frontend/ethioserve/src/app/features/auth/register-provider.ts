import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { catchError, distinctUntilChanged, of, startWith, switchMap } from 'rxjs';
import { toApiError } from '../../core/api/api-error';
import { applyServerErrors } from '../../core/api/apply-server-errors';
import { MarketplaceApi } from '../../core/api/marketplace.api';
import { AuthService } from '../../core/auth/auth.service';
import { homeRouteFor } from '../../core/auth/role-home';
import { PASSWORD_HINT, PHONE_PATTERN, matchingFields, strongPassword } from '../../core/auth/validators';
import { City, Region } from '../../core/models/marketplace.models';

@Component({
  selector: 'app-register-provider',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <section class="card narrow">
      <h1>Offer your services</h1>
      <p class="muted">
        Create a provider account, complete your profile, and our team will verify it before customers can find you.
        Looking to hire instead? <a routerLink="/register">Create a customer account</a>.
      </p>

      @if (errorMessage(); as message) {
        <p class="alert alert-error" role="alert">{{ message }}</p>
      }

      <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
        <div class="field">
          <label for="businessName">Business or professional name</label>
          <input
            id="businessName"
            formControlName="businessName"
            autocomplete="organization"
            [attr.aria-invalid]="form.controls.businessName.touched && form.controls.businessName.invalid"
          />
          @if (form.controls.businessName.touched && form.controls.businessName.invalid) {
            <p class="field-error">{{ form.controls.businessName.errors?.['server'] ?? 'Enter your business name.' }}</p>
          }
        </div>

        <div class="field">
          <label for="email">Email</label>
          <input
            id="email"
            type="email"
            formControlName="email"
            autocomplete="email"
            [attr.aria-invalid]="form.controls.email.touched && form.controls.email.invalid"
          />
          @if (form.controls.email.touched && form.controls.email.invalid) {
            <p class="field-error">{{ form.controls.email.errors?.['server'] ?? 'Enter a valid email address.' }}</p>
          }
        </div>

        <div class="field">
          <label for="phoneNumber">Phone number</label>
          <input
            id="phoneNumber"
            type="tel"
            inputmode="tel"
            formControlName="phoneNumber"
            autocomplete="tel"
            aria-describedby="phone-hint"
            [attr.aria-invalid]="form.controls.phoneNumber.touched && form.controls.phoneNumber.invalid"
          />
          <p class="hint" id="phone-hint">For example 0911223344 or +251911223344.</p>
          @if (form.controls.phoneNumber.touched && form.controls.phoneNumber.invalid) {
            <p class="field-error">
              {{ form.controls.phoneNumber.errors?.['server'] ?? 'Enter 9 to 15 digits, with no spaces.' }}
            </p>
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
          <label for="password">Password</label>
          <input
            id="password"
            type="password"
            formControlName="password"
            autocomplete="new-password"
            aria-describedby="password-hint"
            [attr.aria-invalid]="form.controls.password.touched && form.controls.password.invalid"
          />
          <p class="hint" id="password-hint">{{ passwordHint }}</p>
          @if (form.controls.password.touched && form.controls.password.invalid) {
            <p class="field-error">{{ form.controls.password.errors?.['server'] ?? passwordHint }}</p>
          }
        </div>

        <div class="field">
          <label for="confirmPassword">Confirm password</label>
          <input
            id="confirmPassword"
            type="password"
            formControlName="confirmPassword"
            autocomplete="new-password"
            [attr.aria-invalid]="form.controls.confirmPassword.touched && !!form.errors?.['mismatch']"
          />
          @if (form.controls.confirmPassword.touched && form.errors?.['mismatch']) {
            <p class="field-error">The passwords do not match.</p>
          }
        </div>

        <button type="submit" class="btn btn-primary btn-block" [disabled]="submitting()">
          {{ submitting() ? 'Creating account...' : 'Create provider account' }}
        </button>
      </form>

      <p class="form-links"><span>Already have an account?</span> <a routerLink="/login">Sign in</a></p>
    </section>
  `,
})
export class RegisterProvider {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly market = inject(MarketplaceApi);

  protected readonly passwordHint = PASSWORD_HINT;
  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly form = this.fb.nonNullable.group(
    {
      businessName: ['', [Validators.required, Validators.maxLength(150)]],
      email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
      phoneNumber: ['', [Validators.required, Validators.pattern(PHONE_PATTERN)]],
      regionId: [0, [Validators.required, Validators.min(1)]],
      cityId: [0, [Validators.required, Validators.min(1)]],
      password: ['', [Validators.required, strongPassword]],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: [matchingFields('password', 'confirmPassword')] },
  );

  protected readonly regions = toSignal(this.market.regions().pipe(catchError(() => of<Region[]>([]))), {
    initialValue: [] as Region[],
  });

  // The city list always follows the selected region.
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

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set(null);

    const { email, password, businessName, phoneNumber, regionId, cityId } = this.form.getRawValue();

    this.auth
      .registerProvider({
        email: email.trim(),
        password,
        businessName: businessName.trim(),
        phoneNumber: phoneNumber.trim(),
        regionId,
        cityId,
      })
      .subscribe({
        next: (user) => void this.router.navigateByUrl(homeRouteFor(user.roles)),
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