import { Component, input, output } from '@angular/core';
import { UiCard } from './card.component';
import { DialogService } from './dialog';

@Component({
  selector: 'ui-confirm-dialog',
  standalone: true,
  imports: [UiCard],
  template: `
    <ui-card class="ui-confirm-card">
      <h2 class="ui-confirm-title">{{ title() }}</h2>
      @if (message()) {
        <p class="ui-confirm-message">{{ message() }}</p>
      }
      <div class="ui-confirm-actions">
        <button type="button" class="ui-btn ui-btn-secondary" (click)="dismiss.emit()">Cancel</button>
        <button type="button" class="ui-btn ui-btn-danger" (click)="confirmed.emit()">{{ dangerLabel() }}</button>
      </div>
    </ui-card>
  `,
  styles: `
    .ui-confirm-card {
      max-width: 420px;
    }
    .ui-confirm-title {
      margin: 0 0 0.5rem;
      font-size: 1.25rem;
    }
    .ui-confirm-message {
      color: var(--color-muted);
      margin: 0 0 1.5rem;
      font-size: 0.95rem;
    }
    .ui-confirm-actions {
      display: flex;
      justify-content: flex-end;
      gap: 0.75rem;
    }
  `,
})
export class UiConfirmDialog {
  readonly title = input<string>('Are you sure?');
  readonly message = input<string>('');
  readonly dangerLabel = input<string>('Delete');
  readonly confirmed = output<void>();
  readonly dismiss = output<void>();
}

export function confirmDialog(dialog: DialogService, title: string, message: string, dangerLabel = 'Delete'): Promise<boolean> {
  return dialog.open(`${title}\n${message}`).then((result) => result);
}
