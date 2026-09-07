import { describe, expect, it } from 'vitest';
import { toggleId } from './compare.store';

describe('toggleId', () => {
  it('adds a room that is not chosen yet', () => {
    expect(toggleId([], 4, 3)).toEqual([4]);
  });

  it('removes one that already is', () => {
    expect(toggleId([1, 4], 4, 3)).toEqual([1]);
  });

  /** Rule 11: the fourth is a 422, so the interface never sends it. */
  it('refuses a fourth room rather than dropping one', () => {
    expect(toggleId([1, 2, 3], 7, 3)).toEqual([1, 2, 3]);
  });

  it('still lets a chosen room be removed at the cap', () => {
    expect(toggleId([1, 2, 3], 2, 3)).toEqual([1, 3]);
  });
});
