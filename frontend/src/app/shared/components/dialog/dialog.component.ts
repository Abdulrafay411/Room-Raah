import {
  Component,
  ElementRef,
  EventEmitter,
  HostListener,
  Input,
  OnChanges,
  OnDestroy,
  Output,
  SimpleChanges,
  ViewChild,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { SpinnerComponent } from '../spinner/spinner.component';
import { ButtonComponent } from '../button/button.component';

let nextDialogId = 0;

@Component({
  selector: 'app-dialog',
  standalone: true,
  imports: [CommonModule, SpinnerComponent, ButtonComponent],
  templateUrl: './dialog.component.html',
  styleUrls: ['./dialog.component.scss'],
})
export class DialogComponent implements OnChanges, OnDestroy {
  private dialogId = `rr-dialog-${++nextDialogId}`;

  @Input() isOpen: boolean = false;
  @Input() title: string = '';
  @Input() description: string = '';
  @Input() role: 'dialog' | 'alertdialog' = 'dialog';
  @Input() disableClose: boolean = false;
  @Input() closeOnBackdropClick: boolean = true;
  @Input() loading: boolean = false;
  @Input() maxWidth: string = '540px';

  @Output() isOpenChange = new EventEmitter<boolean>();
  @Output() closed = new EventEmitter<void>();

  @ViewChild('dialogPanel') dialogPanel?: ElementRef<HTMLElement>;

  get titleId(): string {
    return `${this.dialogId}-title`;
  }

  get descriptionId(): string {
    return `${this.dialogId}-description`;
  }

  private previousActiveElement: HTMLElement | null = null;

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['isOpen']) {
      if (this.isOpen) {
        this.handleOpen();
      } else {
        this.handleClose();
      }
    }
  }

  ngOnDestroy(): void {
    if (this.isOpen) {
      this.unlockBodyScroll();
      this.restoreFocus();
    }
  }

  private handleOpen(): void {
    if (typeof document !== 'undefined') {
      this.previousActiveElement = document.activeElement as HTMLElement;
      this.lockBodyScroll();

      // Defer focus setting until dialog element is rendered
      setTimeout(() => {
        this.trapFocusInitial();
      }, 50);
    }
  }

  private handleClose(): void {
    this.unlockBodyScroll();
    this.restoreFocus();
  }

  close(): void {
    if (this.disableClose || this.loading) return;
    this.isOpen = false;
    this.isOpenChange.emit(false);
    this.closed.emit();
    this.handleClose();
  }

  onBackdropClick(event: MouseEvent): void {
    if (event.target === event.currentTarget && this.closeOnBackdropClick) {
      this.close();
    }
  }

  @HostListener('document:keydown.escape', ['$event'])
  onEscapeKey(event: Event): void {
    if (this.isOpen && !this.disableClose && !this.loading) {
      event.preventDefault();
      this.close();
    }
  }

  @HostListener('keydown', ['$event'])
  onKeydown(event: KeyboardEvent): void {
    if (!this.isOpen || event.key !== 'Tab') return;

    const focusable = this.getFocusableElements();
    if (focusable.length === 0) {
      event.preventDefault();
      return;
    }

    const firstElement = focusable[0];
    const lastElement = focusable[focusable.length - 1];

    if (event.shiftKey) {
      // Shift + Tab: if on first element, wrap to last
      if (document.activeElement === firstElement || !this.dialogPanel?.nativeElement.contains(document.activeElement)) {
        event.preventDefault();
        lastElement.focus();
      }
    } else {
      // Tab: if on last element, wrap to first
      if (document.activeElement === lastElement || !this.dialogPanel?.nativeElement.contains(document.activeElement)) {
        event.preventDefault();
        firstElement.focus();
      }
    }
  }

  private getFocusableElements(): HTMLElement[] {
    if (!this.dialogPanel?.nativeElement) return [];

    const selector =
      'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';
    const elements = Array.from(
      this.dialogPanel.nativeElement.querySelectorAll<HTMLElement>(selector)
    ).filter((el) => el.offsetParent !== null && !el.hasAttribute('disabled'));

    return elements;
  }

  private trapFocusInitial(): void {
    const focusable = this.getFocusableElements();
    if (focusable.length > 0) {
      // Look for element with autofocus attribute first
      const autoFocusEl = focusable.find((el) => el.hasAttribute('autofocus'));
      if (autoFocusEl) {
        autoFocusEl.focus();
      } else {
        focusable[0].focus();
      }
    } else if (this.dialogPanel?.nativeElement) {
      this.dialogPanel.nativeElement.focus();
    }
  }

  private restoreFocus(): void {
    if (this.previousActiveElement && typeof this.previousActiveElement.focus === 'function') {
      try {
        this.previousActiveElement.focus();
      } catch {
        // Safe fallback if previous element was unmounted
      }
      this.previousActiveElement = null;
    }
  }

  private lockBodyScroll(): void {
    if (typeof document !== 'undefined') {
      document.body.style.overflow = 'hidden';
    }
  }

  private unlockBodyScroll(): void {
    if (typeof document !== 'undefined') {
      document.body.style.overflow = '';
    }
  }
}
