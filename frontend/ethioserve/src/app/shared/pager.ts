import { Component, input, output } from '@angular/core';

@Component({
  selector: 'app-pager',
  template: `
    @if (totalPages() > 1) {
      <nav class="pager" aria-label="Pagination">
        <button type="button" class="btn btn-secondary" [disabled]="page() <= 1" (click)="go(page() - 1)">
          Previous
        </button>
        <span aria-live="polite">Page {{ page() }} of {{ totalPages() }}</span>
        <button type="button" class="btn btn-secondary" [disabled]="page() >= totalPages()" (click)="go(page() + 1)">
          Next
        </button>
      </nav>
    }
  `,
})
export class Pager {
  readonly page = input.required<number>();
  readonly totalPages = input.required<number>();
  readonly pageChange = output<number>();

  protected go(target: number): void {
    if (target >= 1 && target <= this.totalPages() && target !== this.page()) {
      this.pageChange.emit(target);
    }
  }
}