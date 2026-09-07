import { Injectable, computed, signal } from '@angular/core';
import { COMPARE_LIMIT } from './property.service';

const KEY = 'roomraah.compare';

/**
 * Which rooms a person has picked to compare, held for the whole session.
 *
 * The compare page reads its ids from the query string, which is right - a comparison is a
 * thing people send each other. But that left the page with no way in except typing a URL:
 * the only link to it was on the shortlist, so anybody not signed in never saw the feature
 * at all. This is the missing half - the picking - and it lives in one place because three
 * screens do it: search results, a listing, and the shortlist.
 *
 * It survives a reload because choosing rooms and then opening one to look at it properly
 * is the obvious way to use this, and losing the selection on the way back would make the
 * feature feel broken rather than finished.
 */
@Injectable({ providedIn: 'root' })
export class CompareStore {
  private readonly selected = signal<readonly number[]>(restore());

  readonly limit = COMPARE_LIMIT;
  readonly ids = this.selected.asReadonly();
  readonly count = computed(() => this.selected().length);
  readonly full = computed(() => this.selected().length >= COMPARE_LIMIT);
  /** `?ids=` for the compare page. */
  readonly query = computed(() => this.selected().join(','));

  has(propertyId: number): boolean {
    return this.selected().includes(propertyId);
  }

  /** Blocked at the cap rather than silently dropping the fourth, which is rule 11. */
  toggle(propertyId: number): void {
    this.selected.update((ids) => toggleId(ids, propertyId, COMPARE_LIMIT));
    persist(this.selected());
  }

  clear(): void {
    this.selected.set([]);
    persist([]);
  }
}

export function toggleId(ids: readonly number[], id: number, limit: number): readonly number[] {
  if (ids.includes(id)) return ids.filter((value) => value !== id);
  if (ids.length >= limit) return ids;
  return [...ids, id];
}

function restore(): readonly number[] {
  try {
    const raw = localStorage.getItem(KEY);
    if (!raw) return [];
    const parsed: unknown = JSON.parse(raw);
    if (!Array.isArray(parsed)) return [];
    return parsed
      .filter((value): value is number => Number.isInteger(value) && value > 0)
      .slice(0, COMPARE_LIMIT);
  } catch {
    return [];
  }
}

function persist(ids: readonly number[]): void {
  try {
    localStorage.setItem(KEY, JSON.stringify(ids));
  } catch {
    // A browser with storage switched off still gets a working selection for this page.
  }
}
