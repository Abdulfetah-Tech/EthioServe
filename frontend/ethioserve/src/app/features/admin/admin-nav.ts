import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

@Component({
  selector: 'app-admin-nav',
  imports: [RouterLink, RouterLinkActive],
  template: `
    <nav class="tabs" aria-label="Admin area">
      <a routerLink="/admin" routerLinkActive="active" ariaCurrentWhenActive="page" [routerLinkActiveOptions]="{ exact: true }">
        Overview
      </a>
      <a routerLink="/admin/providers" routerLinkActive="active" ariaCurrentWhenActive="page">Providers</a>
    </nav>
  `,
})
export class AdminNav {}