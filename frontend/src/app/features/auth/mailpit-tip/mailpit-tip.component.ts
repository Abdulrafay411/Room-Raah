import { Component } from '@angular/core';
import { environment } from '../../../../environments/environment';

/** Mailpit runs beside the API on the same machine, on its own port. */
export function mailpitUrl(apiBaseUrl: string): string {
  const api = new URL(apiBaseUrl);
  return `${api.protocol}//${api.hostname}:8025`;
}

/**
 * Every email the backend sends - sign-up codes, password resets - lands in Mailpit
 * rather than a real inbox, so the auth screens that wait on one link straight to it.
 */
@Component({
  selector: 'app-mailpit-tip',
  standalone: true,
  template: `
    <div class="mailpit-tip-box">
      <p class="tip-title">Open Mailpit <span class="tip-note">(this button is only for testing)</span></p>
      <a
        class="mailpit-button"
        [href]="url"
        target="_blank"
        rel="noopener noreferrer"
        data-smoke="open-mailpit"
      >
        Open Mailpit <span class="sr-only">(opens in a new tab)</span>
      </a>
    </div>
  `,
  styleUrls: ['./mailpit-tip.component.scss'],
})
export class MailpitTipComponent {
  readonly url = mailpitUrl(environment.apiBaseUrl);
}
