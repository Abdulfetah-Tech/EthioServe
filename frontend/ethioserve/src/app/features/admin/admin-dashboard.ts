import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { AdminApi } from '../../core/api/admin.api';
import { LOADING, toLoadState } from '../../core/api/load-state';
import { AuthService } from '../../core/auth/auth.service';
import { AdminNav } from './admin-nav';

@Component({
  selector: 'app-admin-dashboard',
  imports: [RouterLink, AdminNav],
  template: `
    <div class="stack">
      <header>
        <h1>Administration</h1>
        <p class="muted">Signed in as {{ auth.currentUser()?.email }}</p>
      </header>

      <app-admin-nav />

      <section class="card" aria-labelledby="review-heading">
        <h2 id="review-heading">Provider verification</h2>

        @let waiting = pending();
        @if (waiting.kind === 'ready') {
          @if (waiting.data.totalCount === 0) {
            <p>Nothing is waiting for review.</p>
          } @else {
            <p>
              <strong>{{ waiting.data.totalCount }}</strong>
              {{ waiting.data.totalCount === 1 ? 'provider is' : 'providers are' }} waiting for review.
            </p>
          }
          <a routerLink="/admin/providers" class="btn btn-primary">Open the review queue</a>
        } @else if (waiting.kind === 'error') {
          <p class="alert alert-error" role="alert">{{ waiting.message }}</p>
        } @else {
          <p class="muted" role="status">Loading...</p>
        }
      </section>
    </div>
  `,
})
export class AdminDashboard {
  protected readonly auth = inject(AuthService);
  private readonly api = inject(AdminApi);

  protected readonly pending = toSignal(toLoadState(this.api.providers({ status: 'Pending' }, 1)), {
    initialValue: LOADING,
  });
}