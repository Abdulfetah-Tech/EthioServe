import { EtbPipe } from './etb.pipe';

describe('EtbPipe', () => {
  const pipe = new EtbPipe();

  it('formats whole amounts without decimals', () => {
    const text = pipe.transform(1800);

    expect(text).toContain('ETB');
    expect(text).toContain('1,800');
    expect(text).not.toContain('.');
  });

  it('keeps cents when there are some', () => {
    expect(pipe.transform(150.5)).toContain('150.5');
  });

  it('returns an empty string for missing values', () => {
    expect(pipe.transform(null)).toBe('');
    expect(pipe.transform(undefined)).toBe('');
  });
});