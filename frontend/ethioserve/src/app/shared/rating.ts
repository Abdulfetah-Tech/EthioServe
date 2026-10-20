import { DecimalPipe } from '@angular/common';
import { Component, computed, input } from '@angular/core';

@Component({
  selector: 'app-rating',
  imports: [DecimalPipe],
  template: `
    @if (count() > 0) {
      <span class="rating" role="img" [attr.aria-label]="label()">
        <span aria-hidden="true">&#9733; {{ value() | number: '1.1-1' }} ({{ count() }})</span>
      </span>
    } @else {
      <span class="muted">No reviews yet</span>
    }
  `,
})
export class Rating {
  readonly value = input.required<number>();
  readonly count = input.required<number>();

  protected readonly label = computed(
    () => `Rated ${this.value().toFixed(1)} out of 5 from ${this.count()} reviews`,
  );
}