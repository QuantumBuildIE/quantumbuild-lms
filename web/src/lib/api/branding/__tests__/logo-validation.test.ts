import { describe, it, expect } from 'vitest';
import { getLogoWarnings, validateLogoFile, MAX_LOGO_BYTES } from '../logo-validation';

describe('getLogoWarnings', () => {
  it('has no warnings for a typical 400x100 logo', () => {
    expect(getLogoWarnings(400, 100)).toEqual([]);
  });

  it('warns when under 80px tall, but not at exactly 80px', () => {
    expect(getLogoWarnings(320, 79)).toHaveLength(1);
    expect(getLogoWarnings(320, 80)).toEqual([]);
  });

  it('accepts the 1:1 and 5:1 boundaries', () => {
    expect(getLogoWarnings(100, 100)).toEqual([]);
    expect(getLogoWarnings(500, 100)).toEqual([]);
  });

  it('warns outside 1:1 to 5:1', () => {
    expect(getLogoWarnings(501, 100)).toHaveLength(1); // too wide
    expect(getLogoWarnings(99, 100)).toHaveLength(1); // portrait
  });

  it('can raise both warnings at once', () => {
    expect(getLogoWarnings(600, 60)).toHaveLength(2);
  });
});

describe('validateLogoFile', () => {
  it('accepts PNG and JPEG up to 1 MB', () => {
    expect(validateLogoFile({ type: 'image/png', size: MAX_LOGO_BYTES })).toBeNull();
    expect(validateLogoFile({ type: 'image/jpeg', size: 10 })).toBeNull();
  });

  it('rejects other types and files over 1 MB', () => {
    expect(validateLogoFile({ type: 'image/svg+xml', size: 10 })).toMatch(/PNG or JPEG/);
    expect(validateLogoFile({ type: 'image/png', size: MAX_LOGO_BYTES + 1 })).toMatch(/1 MB/);
  });
});
