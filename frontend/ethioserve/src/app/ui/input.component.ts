import { Component, input, output, model } from '@angular/core';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'ui-input',
  standalone: true,
  imports: [FormsModule],
  template: `
    <div class="ui-field">
      @if (label()) {
        <label class="ui-field-label" [for]="id()">{{ label() }}</label>
      }
      <input
        [id]="id()"
        type="{{ type() }}"
        class="ui-input"
        [class.ui-input-error]="error()"
        [attr.aria-invalid]="error() || undefined"
        [placeholder]="placeholder()"
        [value]="model()"
        (input)="model.set($any($event.target).value)"
      />
      @if (hint()) {
        <p class="ui-field-hint">{{ hint() }}</p>
      }
      @if (error()) {
        <p class="ui-field-error">{{ error() }}</p>
      }
    </div>
  `,
  styles: `
    .ui-field {
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
      margin-bottom: 1rem;
    }
    .ui-field-label {
      font-weight: 600;
      font-size: 0.95rem;
      margin: 0;
    }
    .ui-input {
      padding: 0.6rem 0.75rem;
      border: 1px solid var(--color-border);
      border-radius: 8px;
      background: var(--color-bg);
      color: var(--color-text);
      font: inherit;
      font-size: 1rem;
    }
    .ui-input:focus-visible {
      outline: 3px solid var(--color-primary);
      outline-offset: 2px;
      border-color: var(--color-primary);
    }
    .ui-input-error {
      border-color: var(--color-danger);
    }
    .ui-field-hint {
      color: var(--color-muted);
      font-size: 0.85rem;
      margin: 0;
    }
    .ui-field-error {
      color: var(--color-danger);
      font-size: 0.85rem;
      margin: 0;
    }
  `,
})
export class UiInput {
  readonly model = model<string>('');
  readonly label = input<string>('');
  readonly placeholder = input<string>('');
  readonly type = input<'text' | 'email' | 'password' | 'tel' | 'search' | 'number'>('text');
  readonly error = input<string>('');
  readonly hint = input<string>('');
  readonly id = input<string>('');
}
