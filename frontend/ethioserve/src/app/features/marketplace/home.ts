import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { homeRouteFor } from '../../core/auth/role-home';

@Component({
  selector: 'app-home',
  imports: [RouterLink],
  template: `
    <section class="hero">
      <h1>Find trusted professionals. Get the job done.</h1>
      <p class="muted">
        EthioServe connects you with verified plumbers, electricians, cleaners and more. Describe the job, compare
        quotes, book a time and pay securely.
      </p>

      <div class="hero-actions">
        @if (auth.isAuthenticated()) {
          <a [routerLink]="home()" class="btn btn-primary">Go to my dashboard</a>
        } @else {
          <a routerLink="/register" class="btn btn-primary">Create an account</a>
          <a routerLink="/login" class="btn btn-secondary">Sign in</a>
        }
      </div>
    </section>
  `,
})
export class Home {
  protected readonly auth = inject(AuthService);
  protected readonly home = computed(() => homeRouteFor(this.auth.roles()));
}