import { Component, inject } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { combineLatest, map, scan, switchMap } from 'rxjs';
import { LOADING, LoadState, toLoadState } from '../../core/api/load-state';
import { NotificationApi } from '../../core/api/notification.api';
import { AuthService } from '../../core/auth/auth.service';
import { PagedResult } from '../../core/models/marketplace.models';
import { AppNotification } from '../../core/models/notification.models';
import { NotificationService } from '../../core/notifications/notification.service';
import { Pager } from '../../shared/pager';
import { categoryLabel, timeAgo } from '../../shared/notification-format';
import { notificationRoute } from '../../shared/notification-links';
import {
  DEFAULT_NOTIFICATION_PARAMS,
  NotificationListParams,
  notificationParamsToQuery,
  parseNotificationParams,
  withNotificationPage,
} from '../../shared/notification-list-params';

type Results = LoadState<PagedResult<AppNotification>>;

@Component({
  selector: 'app-notifications-page',
  imports: [RouterLink, Pager],
  template: `
    <div class="stack">
      <header class="section-head">
        <div>
          <h1>Notifications</h1>
          <p class="muted">What happened with your requests, quotes and appointments.</p>
        </div>
        <button type="button" class="btn btn-secondary" [disabled]="service.unread() === 0" (click)="service.markAllRead()">
          Mark all as read
        </button>
      </header>

      <nav class="tabs" aria-label="Notification lists">
        <a
          routerLink="/notifications"
          [class.active]="!params().unreadOnly"
          [attr.aria-current]="!params().unreadOnly ? 'page' : null"
          >All</a
        >
        <a
          routerLink="/notifications"
          [queryParams]="{ unread: 'true' }"
          [class.active]="params().unreadOnly"
          [attr.aria-current]="params().unreadOnly ? 'page' : null"
          >Unread ({{ service.unread() }})</a
        >
      </nav>

      @let res = results();
      @if (res.kind === 'loading') {
        <p class="muted" role="status">Loading notifications...</p>
      } @else if (res.kind === 'error') {
        <p class="alert alert-error" role="alert">{{ res.message }}</p>
      } @else {
        @if (res.data.items.length === 0) {
          <div class="card">
            <p><strong>{{ params().unreadOnly ? 'Nothing unread.' : 'No notifications yet.' }}</strong></p>
            <p class="muted">
              {{ params().unreadOnly ? 'You are all caught up.' : 'You will be told here when something happens.' }}
            </p>
          </div>
        } @else {
          <ul class="notice-list" role="list">
            @for (item of res.data.items; track item.id) {
              <li class="card notice" [class.notice-unread]="!item.isRead">
                <button type="button" class="notice-main" (click)="open(item)">
                  <span class="muted">{{ category(item.type) }} &middot; {{ ago(item.createdAt) }}</span>
                  @if (!item.isRead) {
                    <span class="visually-hidden">Unread</span>
                  }
                  <strong>{{ item.title }}</strong>
                  <span>{{ item.message }}</span>
                </button>
              </li>
            }
          </ul>
        }

        <app-pager [page]="res.data.page" [totalPages]="res.data.totalPages" (pageChange)="goToPage($event)" />
      }
    </div>
  `,
})
export class NotificationsPage {
  private readonly api = inject(NotificationApi);
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly service = inject(NotificationService);
  protected readonly category = categoryLabel;
  protected readonly ago = (iso: string): string => timeAgo(iso);

  private readonly params$ = this.route.queryParamMap.pipe(map((query) => parseNotificationParams(query)));
  protected readonly params = toSignal(this.params$, { initialValue: DEFAULT_NOTIFICATION_PARAMS });

  // Reloads when the address changes or when notifications change. The list on screen stays while it refreshes.
  protected readonly results = toSignal(
    combineLatest([this.params$, toObservable(this.service.version)]).pipe(
      switchMap(([p]) => toLoadState(this.api.list({ unreadOnly: p.unreadOnly, page: p.page }, 15))),
      scan<Results, Results>((previous, next) => (next.kind === 'loading' && previous.kind === 'ready' ? previous : next), LOADING),
    ),
    { initialValue: LOADING },
  );

  protected open(item: AppNotification): void {
    this.service.markRead(item);

    void this.router.navigateByUrl(notificationRoute(item, this.auth.roles()) ?? '/notifications');
  }

  protected goToPage(page: number): void {
    const next: NotificationListParams = withNotificationPage(this.params(), page);

    void this.router.navigate([], { relativeTo: this.route, queryParams: notificationParamsToQuery(next) });
  }
}