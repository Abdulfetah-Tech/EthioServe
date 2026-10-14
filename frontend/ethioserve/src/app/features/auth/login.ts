import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { toApiError } from '../../core/api/api-error';
import { AuthService } from '../../core/auth/auth.service';
import { homeRouteFor } from '../../core/auth/role-home';
import { safeReturnUrl } from '../../core/auth/safe-url';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <section class="card narrow">
      <h1>Sign in</h1>
      <p class="muted">Welcome back to EthioServe.</p>

      @if (errorMessage(); as message) {
        <p class="alert alert-error" role="alert">{{ message }}</p>
      }

      <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
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
            <p class="field-error">Enter a valid email address.</p>
          }
        </div>

        <div class="field">
          <label for="password">Password</label>
          <input
            id="password"
            type="password"
            formControlName="password"
            autocomplete="current-password"
            [attr.aria-invalid]="form.controls.password.touched && form.controls.password.invalid"
          />
          @if (form.controls.password.touched && form.controls.password.invalid) {
            <p class="field-error">Enter your password.</p>
          }
        </div>

        <button type="submit" class="btn btn-primary btn-block" [disabled]="submitting()">
          {{ submitting() ? 'Signing in...' : 'Sign in' }}
        </button>
      </form>

      <p class="form-links">
        <a routerLink="/forgot-password">Forgot your password?</a>
        <a routerLink="/register">Create an account</a>
      </p>
    </section>
  `,
})
export class Login {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]],
  });

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set(null);

    this.auth.login(this.form.getRawValue()).subscribe({
      next: (user) => {
        const returnUrl = safeReturnUrl(this.route.snapshot.queryParamMap.get('returnUrl'));
        void this.router.navigateByUrl(returnUrl ?? homeRouteFor(user.roles));
      },
      error: (error: unknown) => {
        this.errorMessage.set(toApiError(error).message);
        this.submitting.set(false);
      },
    });
  }
}