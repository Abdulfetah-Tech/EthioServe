import { PricingType } from '../../core/models/admin-catalog.models';

export const MAX_PRICE = 10_000_000;

export const PRICING_OPTIONS: readonly { value: PricingType; label: string }[] = [
  { value: 'Fixed', label: 'Fixed price' },
  { value: 'Hourly', label: 'Per hour' },
  { value: 'StartingFrom', label: 'Starting from' },
  { value: 'Negotiable', label: 'Negotiable (no price)' },
];

/** Every pricing type except Negotiable carries a base price. Mirrors the API's rule. */
export function needsPrice(type: PricingType): boolean {
  return type !== 'Negotiable';
}

export function pricingLabel(type: PricingType): string {
  return PRICING_OPTIONS.find((option) => option.value === type)?.label ?? type;
}

/** A negotiable service never sends a price, whatever was typed before the type was switched. */
export function normalizePrice(type: PricingType, price: number | null): number | null {
  return needsPrice(type) ? price : null;
}

/** The message to show for an invalid price, or null when it is fine. The API still validates everything. */
export function priceProblem(type: PricingType, price: number | null): string | null {
  if (!needsPrice(type)) {
    return price === null ? null : 'A negotiable service cannot have a base price.';
  }

  if (price === null || Number.isNaN(price)) {
    return 'Enter a base price.';
  }

  if (price < 0) {
    return 'The price cannot be negative.';
  }

  if (price > MAX_PRICE) {
    return `The price cannot be more than ${MAX_PRICE.toLocaleString('en-US')}.`;
  }

  if (Math.abs(price * 100 - Math.round(price * 100)) > 1e-6) {
    return 'Use at most 2 decimal places.';
  }

  return null;
}