import { Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth/auth.service';
import { homeRouteFor } from './core/auth/role-home';
import { NotificationBell } from './shared/notification-bell';
import { UiButton } from './ui/button.component';

@Component({
  imports: [RouterOutlet, RouterLink, RouterLinkActive, NotificationBell, UiButton],
  selector: 'app-root',
  styleUrl: './app.scss',
  templateUrl: './app.html',
})
export class App {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly home = computed(() => homeRouteFor(this.auth.roles()));
  protected readonly openNav = signal(false);

  protected toggleNav(): void {
    this.openNav.update((v) => !v);
  }

  protected signOut(): void {
    this.auth.logout().subscribe(() => void this.router.navigateByUrl('/login'));
  }
}
