import { Component, inject, Injector, input, output, signal } from '@angular/core';
import { Overlay, OverlayConfig, OverlayRef, PositionStrategy } from '@angular/cdk/overlay';
import { ComponentPortal } from '@angular/cdk/portal';

@Component({
  selector: 'ui-toast',
  standalone: true,
  template: `
    <div class="ui-toast" [class.ui-toast-error]="type() === 'error'" [class.ui-toast-success]="type() === 'success'" [class.ui-toast-info]="type() === 'info'">
      <span class="ui-toast-message">{{ message() }}</span>
      <button type="button" class="ui-toast-close" (click)="dismiss.emit()">&times;</button>
    </div>
  `,
  styles: `
    .ui-toast {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 0.75rem;
      padding: 0.75rem 1rem;
      border-radius: 8px;
      box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
      animation: slideIn 0.3s ease-out;
      border: 1px solid var(--color-border);
    }
    .ui-toast-message {
      color: var(--color-text);
      font-size: 0.95rem;
    }
    .ui-toast-close {
      background: none;
      border: none;
      color: var(--color-muted);
      font-size: 1.5rem;
      line-height: 1;
      cursor: pointer;
      padding: 0;
    }
    .ui-toast-close:hover {
      color: var(--color-text);
    }
    .ui-toast-error {
      background: var(--color-danger-bg);
      color: var(--color-danger);
      border-color: var(--color-danger);
    }
    .ui-toast-success {
      background: var(--color-success-bg);
      color: var(--color-success);
      border-color: var(--color-success);
    }
    .ui-toast-info {
      background: var(--color-bg);
      color: var(--color-primary);
      border-color: var(--color-primary);
    }
    @keyframes slideIn {
      from { transform: translateY(20px); opacity: 0; }
      to { transform: translateY(0); opacity: 1; }
    }
    @media (prefers-color-scheme: dark) {
      .ui-toast-info {
        background: var(--color-surface);
        color: var(--color-primary);
        border-color: var(--color-primary);
      }
    }
  `,
})
export class UiToast {
  readonly message = input<string>('');
  readonly type = input<'success' | 'error' | 'info'>('info');
  readonly dismiss = output<void>();
}

export class ToastService {
  private readonly overlay = inject(Overlay);
  private readonly injector = inject(Injector);

  private stack = 0;

  show(message: string, type: 'success' | 'error' | 'info' = 'info', durationMs = 4000): void {
    const positionStrategy = this.overlay.position()
      .global()
      .right('1rem')
      .bottom(`${1.5 + this.stack * 3}rem`);

    const overlayRef = this.overlay.create(new OverlayConfig({
      positionStrategy,
      hasBackdrop: false,
      panelClass: 'cdk-overlay-pane',
    }));

    const portal = new ComponentPortal(UiToast, null, this.injector);
    const componentRef = overlayRef.attach(portal);

    componentRef.setInput('message', message);
    componentRef.setInput('type', type);

    this.stack++;

    componentRef.instance.dismiss.subscribe(() => {
      overlayRef.dispose();
      this.stack = Math.max(0, this.stack - 1);
    });

    setTimeout(() => {
      overlayRef.dispose();
      this.stack = Math.max(0, this.stack - 1);
    }, durationMs);
  }

  success(message: string, durationMs?: number): void {
    this.show(message, 'success', durationMs);
  }

  error(message: string, durationMs?: number): void {
    this.show(message, 'error', durationMs);
  }

  info(message: string, durationMs?: number): void {
    this.show(message, 'info', durationMs);
  }
}
