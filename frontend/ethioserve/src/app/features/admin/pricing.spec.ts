import { PricingType } from '../../core/models/admin-catalog.models';
import { MAX_PRICE, PRICING_OPTIONS, needsPrice, normalizePrice, priceProblem, pricingLabel } from './pricing';

describe('needsPrice', () => {
  it.each(['Fixed', 'Hourly', 'StartingFrom'] as PricingType[])('requires a price for %s', (type) => {
    expect(needsPrice(type)).toBe(true);
  });

  it('does not require a price for Negotiable', () => {
    expect(needsPrice('Negotiable')).toBe(false);
  });
});

describe('normalizePrice', () => {
  it('keeps the price for priced types', () => {
    expect(normalizePrice('Fixed', 300)).toBe(300);
  });

  it('drops the price when the type is Negotiable, even if one was typed earlier', () => {
    expect(normalizePrice('Negotiable', 300)).toBeNull();
  });
});

describe('priceProblem', () => {
  it.each([0, 150.5, 300, MAX_PRICE])('accepts %s for a fixed price', (price) => {
    expect(priceProblem('Fixed', price)).toBeNull();
  });

  it('requires a price for every priced type', () => {
    expect(priceProblem('Fixed', null)).toBe('Enter a base price.');
    expect(priceProblem('Hourly', null)).toBe('Enter a base price.');
    expect(priceProblem('StartingFrom', Number.NaN)).toBe('Enter a base price.');
  });

  it('rejects negative, too large and over-precise prices', () => {
    expect(priceProblem('Fixed', -1)).toContain('negative');
    expect(priceProblem('Fixed', MAX_PRICE + 1)).toContain('more than');
    expect(priceProblem('Fixed', 12.345)).toContain('2 decimal');
  });

  it('forbids a price on a negotiable service and accepts none', () => {
    expect(priceProblem('Negotiable', 100)).toContain('negotiable');
    expect(priceProblem('Negotiable', null)).toBeNull();
  });
});

describe('pricingLabel and PRICING_OPTIONS', () => {
  it('has a readable label for every type', () => {
    expect(PRICING_OPTIONS.map((option) => option.value).sort()).toEqual(['Fixed', 'Hourly', 'Negotiable', 'StartingFrom']);
    expect(pricingLabel('StartingFrom')).toBe('Starting from');
    expect(pricingLabel('Negotiable')).toContain('Negotiable');
  });
});