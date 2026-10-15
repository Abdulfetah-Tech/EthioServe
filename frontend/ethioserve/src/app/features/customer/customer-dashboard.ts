import { Component, inject } from '@angular/core';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-customer-dashboard',
  template: `
    <h1>Welcome, {{ auth.currentUser()?.displayName }}</h1>
    <p class="muted">Your service requests, quotes and appointments will appear here as we build them.</p>

    <section class="card">
      <h2>Your account</h2>
      <p>Signed in as {{ auth.currentUser()?.email }}</p>
    </section>
  `,
})
export class CustomerDashboard {
  protected readonly auth = inject(AuthService);
}