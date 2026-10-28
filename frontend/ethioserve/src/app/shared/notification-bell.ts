import { Component, DestroyRef, ElementRef, inject, signal, viewChild } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../core/auth/auth.service';
import { AppNotification } from '../core/models/notification.models';
import { NotificationService } from '../core/notifications/notification.service';
import { badgeText, bellLabel, categoryLabel, timeAgo } from './notification-format';
import { notificationRoute } from './notification-links';

/** The bell in the header: the unread count, the newest notifications, and a way into each one. */
@Component({
  selector: 'app-notification-bell',
  imports: [RouterLink],
  host: {
    '(document:keydown.escape)': 'onEscape()',
    '(document:click)': 'onDocumentClick($event)',
  },
  template: `
    <div class="bell">
      <button
        #trigger
        type="button"
        class="bell-button"
        aria-haspopup="true"
        aria-controls="notification-panel"
        [attr.aria-expanded]="isOpen()"
        (click)="toggle()"
      >
        <span aria-hidden="true">&#128276;</span>
        @if (service.unread() > 0) {
          <span class="bell-badge" aria-hidden="true">{{ badge(service.unread()) }}</span>
        }
        <span class="visually-hidden">{{ label(service.unread()) }}</span>
      </button>

      <span class="visually-hidden" role="status" aria-live="polite">{{ service.announcement() }}</span>

      @if (isOpen()) {
        <section id="notification-panel" class="bell-panel" aria-label="Notifications">
          <div class="bell-panel-head">
            <strong>Notifications</strong>
            <button type="button" class="btn btn-ghost" [disabled]="service.unread() === 0" (click)="service.markAllRead()">
              Mark all as read
            </button>
          </div>

          @if (service.recent().length === 0) {
            <p class="muted bell-empty">You have no notifications yet.</p>
          } @else {
            <ul class="bell-list" role="list">
              @for (item of service.recent(); track item.id) {
                <li>
                  <button type="button" class="bell-item" [class.bell-item-unread]="!item.isRead" (click)="open(item)">
                    <span class="bell-item-meta">
                      <span class="muted">{{ category(item.type) }} &middot; {{ ago(item.createdAt) }}</span>
                      @if (!item.isRead) {
                        <span class="visually-hidden">Unread</span>
                      }
                    </span>
                    <strong>{{ item.title }}</strong>
                    <span>{{ item.message }}</span>
                  </button>
                </li>
              }
            </ul>
          }

          <div class="bell-panel-foot">
            <a routerLink="/notifications" (click)="close()">See all notifications</a>
          </div>
        </section>
      }
    </div>
  `,
})
export class NotificationBell {
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly trigger = viewChild<ElementRef<HTMLButtonElement>>('trigger');

  protected readonly service = inject(NotificationService);
  protected readonly badge = badgeText;
  protected readonly label = bellLabel;
  protected readonly category = categoryLabel;
  protected readonly ago = (iso: string): string => timeAgo(iso);

  protected readonly isOpen = signal(false);

  constructor() {
    // The bell only exists while someone is signed in, so its life is the life of the connection.
    this.service.start();
    inject(DestroyRef).onDestroy(() => this.service.stop());
  }

  protected toggle(): void {
    const opening = !this.isOpen();

    this.isOpen.set(opening);

    if (opening) {
      this.service.refresh(); // closes any gap before the live connection was ready
    }
  }

  protected close(): void {
    this.isOpen.set(false);
  }

  protected onEscape(): void {
    if (this.isOpen()) {
      this.isOpen.set(false);
      this.trigger()?.nativeElement.focus();
    }
  }

  protected onDocumentClick(event: Event): void {
    if (this.isOpen() && !this.element.nativeElement.contains(event.target as Node)) {
      this.isOpen.set(false);
    }
  }

  protected open(item: AppNotification): void {
    this.service.markRead(item);
    this.close();

    void this.router.navigateByUrl(notificationRoute(item, this.auth.roles()) ?? '/notifications');
  }
}