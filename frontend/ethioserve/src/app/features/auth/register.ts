import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { applyServerErrors } from '../../core/api/apply-server-errors';
import { toApiError } from '../../core/api/api-error';
import { AuthService } from '../../core/auth/auth.service';
import { homeRouteFor } from '../../core/auth/role-home';
import { PASSWORD_HINT, PHONE_PATTERN, matchingFields, strongPassword } from '../../core/auth/validators';

@Component({
  selector: 'app-register',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <section class="card narrow">
      <h1>Create your account</h1>
      <p class="muted">
        Request services and compare quotes from verified professionals. Want to offer services instead? Provider
        sign-up is coming soon.
      </p>

      @if (errorMessage(); as message) {
        <p class="alert alert-error" role="alert">{{ message }}</p>
      }

      <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
        <div class="grid-2">
          <div class="field">
            <label for="firstName">First name</label>
            <input
              id="firstName"
              formControlName="firstName"
              autocomplete="given-name"
              [attr.aria-invalid]="form.controls.firstName.touched && form.controls.firstName.invalid"
            />
            @if (form.controls.firstName.touched && form.controls.firstName.invalid) {
              <p class="field-error">Enter your first name.</p>
            }
          </div>

          <div class="field">
            <label for="lastName">Last name</label>
            <input
              id="lastName"
              formControlName="lastName"
              autocomplete="family-name"
              [attr.aria-invalid]="form.controls.lastName.touched && form.controls.lastName.invalid"
            />
            @if (form.controls.lastName.touched && form.controls.lastName.invalid) {
              <p class="field-error">Enter your last name.</p>
            }
          </div>
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
          {{ submitting() ? 'Creating account...' : 'Create account' }}
        </button>
      </form>

      <p class="form-links"><span>Already have an account?</span> <a routerLink="/login">Sign in</a></p>
    </section>
  `,
})
export class Register {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly passwordHint = PASSWORD_HINT;
  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly form = this.fb.nonNullable.group(
    {
      firstName: ['', [Validators.required, Validators.maxLength(100)]],
      lastName: ['', [Validators.required, Validators.maxLength(100)]],
      email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
      phoneNumber: ['', [Validators.required, Validators.pattern(PHONE_PATTERN)]],
      password: ['', [Validators.required, strongPassword]],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: [matchingFields('password', 'confirmPassword')] },
  );

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set(null);

    const { firstName, lastName, email, phoneNumber, password } = this.form.getRawValue();

    this.auth.registerCustomer({ firstName, lastName, email, phoneNumber, password }).subscribe({
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