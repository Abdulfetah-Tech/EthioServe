import { Component, inject } from '@angular/core';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-admin-dashboard',
  template: `
    <h1>Administration</h1>
    <p class="muted">Provider verification, moderation, reports and the audit log will appear here.</p>

    <section class="card">
      <h2>Your account</h2>
      <p>Signed in as {{ auth.currentUser()?.email }}</p>
    </section>
  `,
})
export class AdminDashboard {
  protected readonly auth = inject(AuthService);
}