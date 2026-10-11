import { Component, computed, input } from '@angular/core';
import { statusLabel, statusTone } from './admin-labels';

/**
 * A status pill that takes the API's enum name and shows the admin the words for it.
 * Pass the raw value; the label and the colour both come from `admin-labels`.
 */
@Component({
  selector: 'app-admin-status',
  standalone: true,
  template: `<span class="status" [attr.data-tone]="tone()">{{ label() }}</span>`,
  styles: [
    `
      :host {
        display: inline-flex;
      }
      .status {
        display: inline-flex;
        align-items: center;
        padding: 0.2rem 0.6rem;
        border-radius: 999px;
        background: #eef2f6;
        color: #334155;
        font-weight: 600;
        font-size: 0.78rem;
        line-height: 1.3;
        white-space: nowrap;
      }
      .status[data-tone='good'] {
        background: #dcfce7;
        color: #166534;
      }
      .status[data-tone='bad'] {
        background: #fee2e2;
        color: #991b1b;
      }
      .status[data-tone='warn'] {
        background: #fef3c7;
        color: #92400e;
      }
      .status[data-tone='info'] {
        background: #dbe8f1;
        color: #123b5d;
      }
    `,
  ],
})
export class AdminStatusComponent {
  readonly value = input.required<string | null | undefined>();
  readonly label = computed(() => statusLabel(this.value()));
  readonly tone = computed(() => statusTone(this.value()));
}
