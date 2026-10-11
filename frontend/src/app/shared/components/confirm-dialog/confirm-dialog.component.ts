import { Component, input, output } from '@angular/core';
import { ButtonComponent } from '../button/button.component';
import { DialogComponent } from '../dialog/dialog.component';

/**
 * What an action that cannot be taken back asks before it runs. The page builds one of these
 * when a button is pressed, the dialog shows it, and `run` is only called from the confirm
 * button.
 */
export interface ConfirmRequest {
  title: string;
  description: string;
  /** The reason or note that will be sent with the action, quoted back so it can be read once more. */
  quote?: string | null;
  confirmLabel: string;
  variant?: 'danger' | 'primary' | 'success';
  run: () => void;
}

/**
 * One confirm step for every action that cannot be taken back - an admin rejecting or
 * unpublishing, an owner sending a listing to review or removing a photograph. Built on the
 * shared dialog, so it traps focus, closes on Escape and returns focus to the button that
 * opened it.
 */
@Component({
  selector: 'app-confirm-dialog',
  standalone: true,
  imports: [DialogComponent, ButtonComponent],
  template: `
    @if (request(); as request) {
      <app-dialog
        [isOpen]="true"
        [role]="'alertdialog'"
        [title]="request.title"
        [description]="request.description"
        [loading]="busy()"
        maxWidth="460px"
        (closed)="cancelled.emit()"
      >
        @if (request.quote) {
          <blockquote class="confirm-quote">{{ request.quote }}</blockquote>
        }
        <div dialog-actions class="confirm-actions">
          <app-button variant="ghost" (clicked)="cancelled.emit()">Cancel</app-button>
          <app-button [variant]="request.variant ?? 'danger'" [loading]="busy()" (clicked)="request.run()">
            {{ request.confirmLabel }}
          </app-button>
        </div>
      </app-dialog>
    }
  `,
  styles: [
    `
      .confirm-quote {
        margin: 0;
        padding: 0.65rem 0.85rem;
        border-left: 3px solid #cbd5e1;
        background: #f8fafc;
        color: #334155;
        white-space: pre-wrap;
      }
      .confirm-actions {
        display: flex;
        justify-content: flex-end;
        gap: 0.5rem;
      }
    `,
  ],
})
export class ConfirmDialogComponent {
  readonly request = input<ConfirmRequest | null>(null);
  readonly busy = input(false);
  readonly cancelled = output<void>();
}
