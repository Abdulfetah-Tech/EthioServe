import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-not-found',
  imports: [RouterLink],
  template: `
    <section class="card narrow">
      <h1>Page not found</h1>
      <p class="muted">The page you are looking for does not exist or has moved.</p>
      <a routerLink="/" class="btn btn-primary">Back to home</a>
    </section>
  `,
})
export class NotFound {}