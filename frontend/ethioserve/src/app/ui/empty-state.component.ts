import { Component, input, output } from '@angular/core';

@Component({
  selector: 'ui-empty-state',
  standalone: true,
  template: `
    <div class="ui-empty-state">
      @if (icon()) {
        <span class="ui-empty-icon" aria-hidden="true">{{ icon() }}</span>
      }
      <h2 class="ui-empty-title">{{ title() }}</h2>
      @if (description()) {
        <p class="ui-empty-desc">{{ description() }}</p>
      }
      @if (actionLabel()) {
        <button class="ui-empty-action ui-btn ui-btn-primary" (click)="action.emit()">{{ actionLabel() }}</button>
      }
      <ng-content />
    </div>
  `,
  styles: `
    .ui-empty-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      text-align: center;
      padding: 3rem 1rem;
    }
    .ui-empty-icon {
      font-size: 3rem;
      margin-bottom: 1rem;
      opacity: 0.5;
    }
    .ui-empty-title {
      margin: 0 0 0.5rem;
      font-size: 1.25rem;
    }
    .ui-empty-desc {
      color: var(--color-muted);
      margin: 0 0 1.5rem;
      max-width: 420px;
    }
    .ui-empty-action {
      margin-top: 0.5rem;
    }
  `,
})
export class UiEmptyState {
  readonly icon = input<string>('');
  readonly title = input<string>('');
  readonly description = input<string>('');
  readonly actionLabel = input<string>('');
  readonly action = output<void>();
}
