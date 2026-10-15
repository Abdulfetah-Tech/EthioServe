import { Component, inject } from '@angular/core';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-provider-dashboard',
  template: `
    <h1>Welcome, {{ auth.currentUser()?.displayName }}</h1>
    <p class="muted">Incoming requests, quotes, jobs and earnings will appear here as we build them.</p>

    <section class="card">
      <h2>Your account</h2>
      <p>Signed in as {{ auth.currentUser()?.email }}</p>
    </section>
  `,
})
export class ProviderDashboard {
  protected readonly auth = inject(AuthService);
}