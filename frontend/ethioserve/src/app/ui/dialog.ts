import { Component, inject, Injector, input, output } from '@angular/core';
import { Overlay, OverlayConfig, OverlayRef } from '@angular/cdk/overlay';
import { ComponentPortal } from '@angular/cdk/portal';

@Component({
  selector: 'ui-dialog',
  standalone: true,
  template: `
    <div class="ui-dialog-panel">
      <div class="ui-dialog-container">
        @if (title()) {
          <h2 class="ui-dialog-title">{{ title() }}</h2>
        }
        <div class="ui-dialog-content"><ng-content /></div>
        <div class="ui-dialog-actions">
          <button type="button" class="ui-btn ui-btn-secondary" (click)="dismiss.emit()">Cancel</button>
          <button type="button" class="ui-btn ui-btn-primary" (click)="confirmed.emit()">Confirm</button>
        </div>
      </div>
    </div>
  `,
  styles: `
    .ui-dialog-panel {
      display: flex;
      align-items: center;
      justify-content: center;
      position: fixed;
      inset: 0;
      z-index: 1000;
      background: rgba(0, 0, 0, 0.4);
      padding: 1rem;
    }
    .ui-dialog-container {
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius);
      width: min(480px, 92vw);
      max-height: 90vh;
      overflow-y: auto;
      box-shadow: 0 8px 24px rgba(0, 0, 0, 0.18);
    }
    .ui-dialog-title {
      margin: 0 0 1rem;
      padding: 0 1.5rem;
      font-size: 1.25rem;
    }
    .ui-dialog-content {
      padding: 0 1.5rem 1rem;
    }
    .ui-dialog-actions {
      display: flex;
      justify-content: flex-end;
      gap: 0.75rem;
      padding: 1rem 1.5rem;
    }
    @media (prefers-color-scheme: dark) {
      .ui-dialog-panel {
        background: rgba(0, 0, 0, 0.6);
      }
    }
  `,
})
export class UiDialog {
  readonly title = input<string>('');
  readonly confirmed = output<void>();
  readonly dismiss = output<void>();
}

export class DialogService {
  private readonly overlay = inject(Overlay);
  private readonly injector = inject(Injector);

  open(title: string): Promise<boolean> {
    return new Promise((resolve) => {
      const overlayRef = this.overlay.create(new OverlayConfig({
        positionStrategy: this.overlay.position().global().centerHorizontally().centerVertically(),
        hasBackdrop: true,
        backdropClass: 'cdk-overlay-transparent-backdrop',
        panelClass: 'cdk-overlay-pane',
      }));

      const portal = new ComponentPortal(UiDialog, null, this.injector);
      const componentRef = overlayRef.attach(portal);

      componentRef.setInput('title', title);
      componentRef.instance.confirmed.subscribe(() => {
        overlayRef.dispose();
        resolve(true);
      });
      componentRef.instance.dismiss.subscribe(() => {
        overlayRef.dispose();
        resolve(false);
      });

      overlayRef.backdropClick().subscribe(() => {
        overlayRef.dispose();
        resolve(false);
      });
    });
  }
}
