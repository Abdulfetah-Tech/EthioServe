import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { toApiError } from '../../core/api/api-error';
import { AuthService } from '../../core/auth/auth.service';
import { PASSWORD_HINT, matchingFields, strongPassword } from '../../core/auth/validators';

@Component({
  selector: 'app-reset-password',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <section class="card narrow">
      <h1>Choose a new password</h1>

      @if (done()) {
        <p class="alert alert-success" role="status">
          Your password has been changed and you were signed out everywhere.
        </p>
        <a routerLink="/login" class="btn btn-primary btn-block">Sign in</a>
      } @else if (!token) {
        <p class="alert alert-error" role="alert">This reset link is incomplete.</p>
        <a routerLink="/forgot-password">Request a new link</a>
      } @else {
        @if (errorMessage(); as message) {
          <p class="alert alert-error" role="alert">
            {{ message }} <a routerLink="/forgot-password">Request a new link</a>
          </p>
        }

        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <div class="field">
            <label for="newPassword">New password</label>
            <input
              id="newPassword"
              type="password"
              formControlName="newPassword"
              autocomplete="new-password"
              aria-describedby="password-hint"
              [attr.aria-invalid]="form.controls.newPassword.touched && form.controls.newPassword.invalid"
            />
            <p class="hint" id="password-hint">{{ passwordHint }}</p>
            @if (form.controls.newPassword.touched && form.controls.newPassword.invalid) {
              <p class="field-error">{{ form.controls.newPassword.errors?.['server'] ?? passwordHint }}</p>
            }
          </div>

          <div class="field">
            <label for="confirmPassword">Confirm new password</label>
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
            {{ submitting() ? 'Saving...' : 'Change password' }}
          </button>
        </form>
      }
    </section>
  `,
})
export class ResetPassword {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);

  protected readonly token = inject(ActivatedRoute).snapshot.queryParamMap.get('token');
  protected readonly passwordHint = PASSWORD_HINT;
  protected readonly submitting = signal(false);
  protected readonly done = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly form = this.fb.nonNullable.group(
    {
      newPassword: ['', [Validators.required, strongPassword]],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: [matchingFields('newPassword', 'confirmPassword')] },
  );

  protected submit(): void {
    if (this.form.invalid || !this.token) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set(null);

    this.auth.resetPassword({ token: this.token, newPassword: this.form.getRawValue().newPassword }).subscribe({
      next: () => this.done.set(true),
      error: (error: unknown) => {
        this.errorMessage.set(toApiError(error).message);
        this.submitting.set(false);
      },
    });
  }
}