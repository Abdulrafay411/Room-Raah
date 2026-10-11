import {
  Component,
  ElementRef,
  HostListener,
  effect,
  input,
  output,
  viewChild,
} from '@angular/core';
import { ButtonComponent } from '../../../shared/components';

/**
 * The shape every admin tab shares: a queue on one side, the thing being decided on the
 * other.
 *
 * On a laptop the two sit side by side and the detail stays put while the queue scrolls.
 * On a tablet or phone the detail slides in over the queue, with a backdrop, a close
 * button and Escape. Either way, opening a detail moves focus to it - for a while it was
 * appended under a fifty-row list, and clicking "Review" appeared to do nothing at all.
 */
@Component({
  selector: 'app-admin-workspace',
  standalone: true,
  imports: [ButtonComponent],
  templateUrl: './admin-workspace.component.html',
  styleUrls: ['./admin-workspace.component.scss'],
})
export class AdminWorkspaceComponent {
  /** Whether something is being looked at. Drives the drawer on narrow screens. */
  readonly detailOpen = input(false);
  /** What the panel is, for the close button and assistive technology: "Listing review". */
  readonly detailLabel = input('Detail');
  /** Shown in the panel on wide screens while nothing is chosen. */
  readonly placeholder = input('Choose something from the queue to work on it here.');
  /**
   * Keep the panel's content rendered even when nothing is chosen - for a tab whose
   * panel is a form rather than a view of the chosen row.
   */
  readonly alwaysShowDetail = input(false);
  readonly closed = output<void>();

  private readonly panel = viewChild<ElementRef<HTMLElement>>('panel');
  private opener: HTMLElement | null = null;

  constructor() {
    effect(() => {
      const open = this.detailOpen();
      if (open) {
        const active = document.activeElement;
        if (active instanceof HTMLElement && active !== document.body) this.opener = active;
        // The panel's content renders on this same tick; focus once it is there.
        setTimeout(() => {
          const element = this.panel()?.nativeElement;
          if (!element) return;
          element.focus({ preventScroll: true });
          element.scrollIntoView({ block: 'nearest' });
          element.scrollTop = 0;
        }, 0);
      }
    });
  }

  close(): void {
    this.closed.emit();
    const back = this.opener;
    this.opener = null;
    if (back && back.isConnected) {
      setTimeout(() => back.focus(), 0);
    }
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (!this.detailOpen()) return;
    // A dialog above the panel owns Escape while it is open.
    if (document.querySelector('[aria-modal="true"]')) return;
    this.close();
  }
}
