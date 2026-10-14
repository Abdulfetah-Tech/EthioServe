import { FormControl, FormGroup } from '@angular/forms';
import { PHONE_PATTERN, matchingFields, strongPassword } from './validators';

describe('strongPassword', () => {
  it('accepts a password with upper case, lower case, a digit and enough length', () => {
    expect(strongPassword(new FormControl('Str0ngPassw0rd'))).toBeNull();
  });

  it.each(['short1A', 'alllowercase123', 'ALLUPPERCASE123', 'NoDigitsHereAtAll'])('rejects %s', (value) => {
    expect(strongPassword(new FormControl(value))).toEqual({ weakPassword: true });
  });

  it('leaves empty values to the required validator', () => {
    expect(strongPassword(new FormControl(''))).toBeNull();
  });
});

describe('matchingFields', () => {
  const group = (a: string, b: string) => new FormGroup({ a: new FormControl(a), b: new FormControl(b) });

  it('passes when both fields are equal', () => {
    expect(matchingFields('a', 'b')(group('x', 'x'))).toBeNull();
  });

  it('reports a mismatch otherwise', () => {
    expect(matchingFields('a', 'b')(group('x', 'y'))).toEqual({ mismatch: true });
  });
});

describe('PHONE_PATTERN', () => {
  it.each(['0911223344', '+251911223344'])('accepts %s', (value) => {
    expect(PHONE_PATTERN.test(value)).toBe(true);
  });

  it.each(['09112abc44', '091 122 3344', '123'])('rejects %s', (value) => {
    expect(PHONE_PATTERN.test(value)).toBe(false);
  });
});