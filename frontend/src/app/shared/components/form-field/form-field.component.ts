import { AfterContentChecked, AfterContentInit, Component, ContentChild, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { InputDirective } from './input.directive';
import { SpinnerComponent } from '../spinner/spinner.component';

let nextUniqueId = 0;

@Component({
  selector: 'app-form-field',
  standalone: true,
  imports: [CommonModule, SpinnerComponent],
  templateUrl: './form-field.component.html',
  styleUrls: ['./form-field.component.scss'],
  host: {
    '[attr.id]': 'null',
  },
})
export class FormFieldComponent implements AfterContentInit, AfterContentChecked {
  private uniqueId = `rr-field-${++nextUniqueId}`;

  @Input() label: string = '';
  @Input() fieldId: string = '';
  @Input() forId?: string;
  @Input() set id(value: string) {
    if (value) {
      this.fieldId = value;
    }
  }
  @Input() hint: string = '';
  @Input() error: string = '';
  @Input() required: boolean = false;
  @Input() loading: boolean = false;
  @Input() disabled: boolean = false;

  @ContentChild(InputDirective) inputDirective?: InputDirective;

  private lastDescribedBy: string | null | undefined;
  private lastInvalid: boolean | undefined;

  ngAfterContentInit(): void {
    this.syncInput();
  }

  // Content-checked rather than a binding, because the projected input is a content child
  // and its attributes are ours to set. That runs on every change detection pass, so the
  // writes are guarded: `error` and `hint` are inputs that rarely change, and touching the
  // DOM on every pass for a value that has not moved is work nobody asked for.
  ngAfterContentChecked(): void {
    this.syncInput();
  }

  private syncInput(): void {
    if (!this.inputDirective) {
      return;
    }

    const describedBy = this.describedBy;
    const invalid = !!this.error;

    if (describedBy !== this.lastDescribedBy) {
      this.lastDescribedBy = describedBy;
      this.inputDirective.setDescribedBy(describedBy);
    }

    if (invalid !== this.lastInvalid) {
      this.lastInvalid = invalid;
      this.inputDirective.setInvalid(invalid);
    }
  }

  get resolvedId(): string {
    return this.forId || this.fieldId || this.uniqueId;
  }

  get hintId(): string {
    return `${this.resolvedId}-hint`;
  }

  get errorId(): string {
    return `${this.resolvedId}-error`;
  }

  get describedBy(): string | null {
    if (this.error) {
      return this.errorId;
    }
    if (this.hint) {
      return this.hintId;
    }
    return null;
  }
}
