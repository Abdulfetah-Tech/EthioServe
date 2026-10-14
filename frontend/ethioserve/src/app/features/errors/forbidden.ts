import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-forbidden',
  imports: [RouterLink],
  template: `
    <section class="card narrow">
      <h1>Access denied</h1>
      <p class="muted">Your account does not have permission to view that page.</p>
      <a routerLink="/" class="btn btn-primary">Back to home</a>
    </section>
  `,
})
export class Forbidden {}