import { Component, input } from '@angular/core';

@Component({
  selector: 'ui-skeleton',
  standalone: true,
  template: `
    <div class="ui-skeleton" [class.ui-skeleton-circle]="shape() === 'circle'" [style.width.%]="width()" [style.height.px]="height()"></div>
  `,
  styles: `
    .ui-skeleton {
      background: var(--color-bg);
      border-radius: 8px;
      overflow: hidden;
      position: relative;
    }
    .ui-skeleton::after {
      content: '';
      position: absolute;
      top: 0;
      left: 0;
      width: 100%;
      height: 100%;
      background: linear-gradient(90deg, transparent, var(--color-border), transparent);
      animation: shimmer 1.5s ease-in-out infinite;
    }
    .ui-skeleton-circle {
      border-radius: 50%;
    }
    @keyframes shimmer {
      0% { transform: translateX(-100%); }
      100% { transform: translateX(100%); }
    }
    @media (prefers-reduced-motion: reduce) {
      .ui-skeleton::after {
        animation: none;
        opacity: 0.4;
      }
    }
  `,
})
export class UiSkeleton {
  readonly width = input(100);
  readonly height = input(16);
  readonly shape = input<'rect' | 'circle'>('rect');
}
