import { MAX_BYTES, MAX_FILES, formatSize, validateFiles } from './attachment-rules';

const file = (name: string, size: number, type: string): File => ({ name, size, type }) as unknown as File;
const photo = (name: string) => file(name, 1000, 'image/png');

describe('validateFiles', () => {
  it('accepts JPEG, PNG and WebP photos', () => {
    const result = validateFiles([file('a.jpg', 10, 'image/jpeg'), file('b.png', 10, 'image/png'), file('c.webp', 10, 'image/webp')], 0);

    expect(result.accepted).toHaveLength(3);
    expect(result.problems).toEqual([]);
  });

  it('refuses other types and says which file', () => {
    const result = validateFiles([file('anim.gif', 10, 'image/gif'), file('logo.svg', 10, 'image/svg+xml'), file('doc.pdf', 10, 'application/pdf')], 0);

    expect(result.accepted).toEqual([]);
    expect(result.problems).toHaveLength(3);
    expect(result.problems[0]).toContain('anim.gif');
  });

  it('refuses empty and oversized files, but accepts exactly the limit', () => {
    const result = validateFiles([file('empty.png', 0, 'image/png'), file('huge.png', MAX_BYTES + 1, 'image/png'), file('exact.png', MAX_BYTES, 'image/png')], 0);

    expect(result.accepted.map((f) => f.name)).toEqual(['exact.png']);
    expect(result.problems).toHaveLength(2);
  });

  it('only fills the places that are left and explains what was skipped', () => {
    const result = validateFiles([photo('1.png'), photo('2.png'), photo('3.png')], MAX_FILES - 1);

    expect(result.accepted.map((f) => f.name)).toEqual(['1.png']);
    expect(result.problems).toEqual(['A request can have at most 5 photos, so 2 photos were not added.']);
  });

  it('adds nothing when the request is already full', () => {
    const result = validateFiles([photo('1.png')], MAX_FILES);

    expect(result.accepted).toEqual([]);
    expect(result.problems[0]).toContain('1 photo was not added');
  });
});

describe('formatSize', () => {
  it('shows bytes, kilobytes and megabytes', () => {
    expect(formatSize(500)).toBe('500 B');
    expect(formatSize(1536)).toBe('1.5 KB');
    expect(formatSize(5 * 1024 * 1024)).toBe('5.0 MB');
  });
});