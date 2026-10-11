import { Component, computed, input, output } from '@angular/core';
import { ButtonComponent } from '../../../shared/components';

/**
 * "Showing 1–20 of 57" with a way to the next twenty. Every admin queue answers in the
 * same page envelope, and for a while each tab showed the first page and threw the count
 * away - fifty rows with no sign there was a fifty-first.
 */
@Component({
  selector: 'app-admin-pager',
  standalone: true,
  imports: [ButtonComponent],
  template: `
    <nav class="pager" [attr.aria-label]="'Pages of ' + noun()">
      <p class="pager-count">
        @if (totalCount() === 0) {
          No {{ noun() }}
        } @else if (totalCount() <= pageSize()) {
          {{ totalCount() }} {{ totalCount() === 1 ? singular() : noun() }}
        } @else {
          Showing {{ first() }}–{{ last() }} of {{ totalCount() }} {{ noun() }}
        }
      </p>
      @if (totalCount() > pageSize()) {
        <div class="pager-actions">
          <app-button size="sm" variant="outline" [disabled]="page() <= 1" (clicked)="go(page() - 1)">
            Previous
          </app-button>
          <app-button size="sm" variant="outline" [disabled]="last() >= totalCount()" (clicked)="go(page() + 1)">
            Next
          </app-button>
        </div>
      }
    </nav>
  `,
  styles: [
    `
      .pager {
        display: flex;
        flex-wrap: wrap;
        align-items: center;
        justify-content: space-between;
        gap: 0.5rem 1rem;
        padding-top: 0.75rem;
        margin-top: 0.75rem;
        border-top: 1px solid #e2e8f0;
      }
      .pager-count {
        margin: 0;
        color: #64748b;
        font-size: 0.9rem;
      }
      .pager-actions {
        display: flex;
        gap: 0.5rem;
      }
    `,
  ],
})
export class AdminPagerComponent {
  readonly page = input.required<number>();
  readonly pageSize = input.required<number>();
  readonly totalCount = input.required<number>();
  /** Plural noun for the count: "listings", "accounts". */
  readonly noun = input('items');
  readonly singular = computed(() => {
    const word = this.noun();
    return word.endsWith('s') ? word.slice(0, -1) : word;
  });
  readonly pageChange = output<number>();

  readonly first = computed(() => (this.page() - 1) * this.pageSize() + 1);
  readonly last = computed(() => Math.min(this.page() * this.pageSize(), this.totalCount()));

  go(page: number): void {
    if (page < 1) return;
    this.pageChange.emit(page);
  }
}
