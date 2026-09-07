import { describe, expect, it } from 'vitest';
import { readIds } from './compare.component';

/**
 * The comparison lives in the query string, which means anybody can hand-edit it. A fourth
 * id is a 422 and a non-numeric one is a 400, so the parser is the thing standing between a
 * pasted URL and an error screen.
 */
describe('reading compare ids from the URL', () => {
  it('reads a comma-separated list', () => {
    expect(readIds('1,2,3')).toEqual([1, 2, 3]);
  });

  it('caps at three, because a fourth id is refused by the server', () => {
    expect(readIds('1,2,3,4,5')).toEqual([1, 2, 3]);
  });

  it('drops duplicates before the cap, so three distinct rooms still arrive', () => {
    expect(readIds('1,1,2,2,3')).toEqual([1, 2, 3]);
  });

  it('ignores anything that is not a positive whole number', () => {
    expect(readIds('1,abc,-4,0,2.5,3')).toEqual([1, 3]);
  });

  it('treats an absent or empty parameter as nothing to compare', () => {
    expect(readIds(undefined)).toEqual([]);
    expect(readIds('')).toEqual([]);
    expect(readIds('   ')).toEqual([]);
  });
});
