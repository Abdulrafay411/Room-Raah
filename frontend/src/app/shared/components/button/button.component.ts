import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SpinnerComponent } from '../spinner/spinner.component';

export type ButtonVariant =
  | 'primary'      // Deep Navy (#123B5D)
  | 'success'      // Green (#16A36A)
  | 'secondary'    // Slate / Soft Background
  | 'outline'      // Navy border & text
  | 'outline-green'// Green border & text
  | 'ghost'        // Transparent
  | 'danger';      // Red (#dc2626)

export type ButtonSize = 'sm' | 'md' | 'lg';

@Component({
  selector: 'app-button',
  standalone: true,
  imports: [CommonModule, SpinnerComponent],
  templateUrl: './button.component.html',
  styleUrls: ['./button.component.scss'],
})
export class ButtonComponent {
  @Input() variant: ButtonVariant = 'primary';
  @Input() size: ButtonSize = 'md';
  @Input() type: 'button' | 'submit' | 'reset' = 'button';
  @Input() disabled: boolean = false;
  @Input() loading: boolean = false;
  @Input() isIconOnly: boolean = false;
  @Input() ariaLabel: string = '';
  @Input() pressed: boolean | null = null; // for toggle buttons (e.g. shortlist)
  @Input() fullWidth: boolean = false;

  @Output() clicked = new EventEmitter<MouseEvent>();

  get computedAriaLabel(): string | null {
    if (this.ariaLabel) {
      return this.ariaLabel;
    }
    return null;
  }

  get spinnerColor(): 'white' | 'navy' | 'green' | 'current' {
    if (this.variant === 'primary' || this.variant === 'success' || this.variant === 'danger') {
      return 'white';
    }
    if (this.variant === 'outline-green') {
      return 'green';
    }
    return 'navy';
  }

  handleClick(event: MouseEvent): void {
    if (this.disabled || this.loading) {
      event.preventDefault();
      event.stopPropagation();
      return;
    }
    this.clicked.emit(event);
  }
}
