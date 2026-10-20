import { Pipe, PipeTransform } from '@angular/core';

/** Formats an amount of Ethiopian birr, for example "ETB 1,800". */
@Pipe({ name: 'etb' })
export class EtbPipe implements PipeTransform {
  private static readonly format = new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'ETB',
    minimumFractionDigits: 0,
    maximumFractionDigits: 2,
  });

  transform(value: number | null | undefined): string {
    return value === null || value === undefined ? '' : EtbPipe.format.format(value);
  }
}