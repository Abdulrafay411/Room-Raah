import { Directive, ElementRef, HostBinding, Input, Renderer2, inject } from '@angular/core';

@Directive({
  selector: 'input[appInput], textarea[appInput], select[appInput]',
  standalone: true,
  host: {
    '[class.form-control]': 'true',
  },
})
export class InputDirective {
  private readonly el = inject(ElementRef<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>);
  private readonly renderer = inject(Renderer2);

  private _hasError = false;
  private injectedInvalid = false;
  private injectedDescribedBy: string | null = null;

  @Input()
  set hasError(val: boolean) {
    this._hasError = val;
    this.updateValidity();
  }
  get hasError(): boolean {
    return this._hasError;
  }

  @Input()
  set ariaInvalid(val: boolean | null | undefined) {
    if (val !== undefined && val !== null) {
      this.injectedInvalid = !!val;
      this.updateValidity();
    }
  }

  @Input()
  set ariaDescribedBy(val: string | null | undefined) {
    if (val !== undefined) {
      this.setDescribedBy(val);
    }
  }

  public setDescribedBy(describedBy: string | null): void {
    this.injectedDescribedBy = describedBy;
    if (describedBy) {
      this.renderer.setAttribute(this.el.nativeElement, 'aria-describedby', describedBy);
    } else {
      this.renderer.removeAttribute(this.el.nativeElement, 'aria-describedby');
    }
  }

  public setInvalid(invalid: boolean): void {
    this.injectedInvalid = invalid;
    this.updateValidity();
  }

  private updateValidity(): void {
    const isInvalid = this._hasError || this.injectedInvalid;
    if (isInvalid) {
      this.renderer.addClass(this.el.nativeElement, 'is-invalid');
      this.renderer.setAttribute(this.el.nativeElement, 'aria-invalid', 'true');
    } else {
      this.renderer.removeClass(this.el.nativeElement, 'is-invalid');
      this.renderer.removeAttribute(this.el.nativeElement, 'aria-invalid');
    }
  }
}


