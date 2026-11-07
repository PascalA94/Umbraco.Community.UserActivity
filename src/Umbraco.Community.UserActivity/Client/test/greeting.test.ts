import { describe, expect, it } from 'vitest';
import { greeting } from '../src/greeting.js';

describe('greeting', () => {
  it('names the package', () => {
    expect(greeting('Demo')).toBe('Demo is installed.');
  });
});
