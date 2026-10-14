import { Component, computed, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth/auth.service';
import { homeRouteFor } from './core/auth/role-home';

@Component({
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  selector: 'app-root',
  styleUrl: './app.scss',
  templateUrl: './app.html',
})
export class App {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly home = computed(() => homeRouteFor(this.auth.roles()));

  protected signOut(): void {
    this.auth.logout().subscribe(() => void this.router.navigateByUrl('/login'));
  }
}