import { Component, input } from '@angular/core';

@Component({
  selector: 'ui-badge',
  standalone: true,
  template: `
    <span class="ui-badge" [class.ui-badge-ok]="variant() === 'ok'" [class.ui-badge-warn]="variant() === 'warn'" [class.ui-badge-danger]="variant() === 'danger'">
      <ng-content />
    </span>
  `,
  styles: `
    .ui-badge {
      display: inline-flex;
      align-items: center;
      padding: 0.15rem 0.6rem;
      border-radius: 999px;
      border: 1px solid var(--color-border);
      color: var(--color-muted);
      font-size: 0.8rem;
      font-weight: 600;
    }
    .ui-badge-ok {
      background: var(--color-success-bg);
      border-color: transparent;
      color: var(--color-success);
    }
    .ui-badge-warn {
      background: #fff4d6;
      border-color: transparent;
      color: #7a5b00;
    }
    .ui-badge-danger {
      background: var(--color-danger-bg);
      border-color: transparent;
      color: var(--color-danger);
    }
    @media (prefers-color-scheme: dark) {
      .ui-badge-warn {
        background: #3a2f10;
        color: #f3d98b;
      }
    }
  `,
})
export class UiBadge {
  readonly variant = input<'ok' | 'warn' | 'danger' | 'muted'>('muted');
}
