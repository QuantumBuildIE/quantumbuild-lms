import { describe, it, expect } from 'vitest';

describe('test framework smoke test', () => {
  it('runs vitest', () => {
    expect(1 + 1).toBe(2);
  });

  it('has jest-dom matchers available', () => {
    const div = document.createElement('div');
    div.textContent = 'hello';
    expect(div).toHaveTextContent('hello');
  });
});
