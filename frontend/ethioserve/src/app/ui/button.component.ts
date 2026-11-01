import { Component, input, output } from '@angular/core';

@Component({
  selector: 'ui-button',
  standalone: true,
  template: `
    <button
      type="{{ type() }}"
      class="ui-btn"
      [class.ui-btn-primary]="variant() === 'primary'"
      [class.ui-btn-secondary]="variant() === 'secondary'"
      [class.ui-btn-ghost]="variant() === 'ghost'"
      [class.ui-btn-danger]="variant() === 'danger'"
      [disabled]="disabled()"
      (click)="clicked.emit()"
    >
      <span class="ui-btn-content"><ng-content /></span>
    </button>
  `,
  styles: `
    .ui-btn {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      gap: 0.5rem;
      padding: 0.6rem 1.1rem;
      border-radius: 8px;
      border: 1px solid transparent;
      font: inherit;
      font-weight: 600;
      text-decoration: none;
      cursor: pointer;
      transition: all 0.2s ease;
      line-height: 1.4;
    }
    .ui-btn:disabled {
      opacity: 0.6;
      cursor: not-allowed;
    }
    .ui-btn:focus-visible {
      outline: 3px solid var(--color-primary);
      outline-offset: 2px;
    }
    .ui-btn-primary {
      background: var(--color-primary);
      color: var(--color-primary-text);
    }
    .ui-btn-primary:hover:not(:disabled) {
      background: var(--color-primary-hover);
    }
    .ui-btn-secondary {
      background: transparent;
      border-color: var(--color-border);
      color: var(--color-text);
    }
    .ui-btn-secondary:hover:not(:disabled) {
      border-color: var(--color-primary);
      background: var(--color-bg);
    }
    .ui-btn-ghost {
      background: transparent;
      color: var(--color-text);
      border-color: var(--color-border);
    }
    .ui-btn-ghost:hover:not(:disabled) {
      border-color: var(--color-primary);
      background: var(--color-bg);
    }
    .ui-btn-danger {
      background: transparent;
      border-color: var(--color-danger);
      color: var(--color-danger);
    }
    .ui-btn-danger:hover:not(:disabled) {
      background: var(--color-danger-bg);
    }
    .ui-btn-content {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 0.5rem;
    }
  `,
})
export class UiButton {
  readonly variant = input<'primary' | 'secondary' | 'ghost' | 'danger'>('primary');
  readonly type = input<'button' | 'submit' | 'reset'>('button');
  readonly disabled = input(false);
  readonly clicked = output<void>();
}
