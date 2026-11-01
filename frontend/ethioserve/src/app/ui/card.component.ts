import { Component, input } from '@angular/core';

@Component({
  selector: 'ui-card',
  standalone: true,
  template: `
    <div class="ui-card"><ng-content /></div>
  `,
  styles: `
    .ui-card {
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius);
      padding: 1.5rem;
    }
  `,
})
export class UiCard {
  readonly padding = input<'none' | 'sm' | 'md' | 'lg'>('md');
}
