import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormFieldComponent } from './form-field.component';
import { InputDirective } from './input.directive';

describe('FormFieldComponent', () => {
  let component: FormFieldComponent;
  let fixture: ComponentFixture<FormFieldComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FormFieldComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(FormFieldComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should not bind id attribute to the host element', () => {
    fixture.componentRef.setInput('fieldId', 'my-field-id');
    fixture.detectChanges();
    expect(fixture.nativeElement.getAttribute('id')).toBeNull();
  });

  it('should render label and associate it with fieldId or id input', () => {
    fixture.componentRef.setInput('label', 'Rent Amount');
    fixture.componentRef.setInput('fieldId', 'rent-input');
    fixture.detectChanges();

    const labelEl = fixture.nativeElement.querySelector('label');
    expect(labelEl).toBeTruthy();
    expect(labelEl.textContent).toContain('Rent Amount');
    expect(labelEl.getAttribute('for')).toBe('rent-input');
    expect(fixture.nativeElement.getAttribute('id')).toBeNull();
  });

  it('should show required indicator when required is true', () => {
    fixture.componentRef.setInput('label', 'City');
    fixture.componentRef.setInput('required', true);
    fixture.detectChanges();

    const requiredStar = fixture.nativeElement.querySelector('.required-indicator');
    expect(requiredStar).toBeTruthy();
  });

  it('should display error message with role="alert" when error is set', () => {
    fixture.componentRef.setInput('error', 'Invalid email address');
    fixture.detectChanges();

    const errorEl = fixture.nativeElement.querySelector('.field-error');
    expect(errorEl).toBeTruthy();
    expect(errorEl.textContent).toContain('Invalid email address');
    expect(errorEl.getAttribute('role')).toBe('alert');
    expect(component.describedBy).toBe(component.errorId);
  });

  it('should display hint when provided and no error exists', () => {
    fixture.componentRef.setInput('hint', 'Include monthly utilities if any');
    fixture.detectChanges();

    const hintEl = fixture.nativeElement.querySelector('.field-hint');
    expect(hintEl).toBeTruthy();
    expect(hintEl.textContent).toContain('Include monthly utilities if any');
    expect(component.describedBy).toBe(component.hintId);
  });
});

@Component({
  standalone: true,
  imports: [FormFieldComponent, InputDirective],
  template: `
    <app-form-field label="Email" fieldId="test-email" [error]="error()" [hint]="hint()">
      <input appInput id="test-email" type="email" />
    </app-form-field>
  `,
})
class TestHostComponent {
  error = signal('');
  hint = signal('');
}

describe('FormFieldComponent with InputDirective integration', () => {
  let fixture: ComponentFixture<TestHostComponent>;
  let host: TestHostComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TestHostComponent, FormFieldComponent, InputDirective],
    }).compileComponents();

    fixture = TestBed.createComponent(TestHostComponent);
    host = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should automatically bind aria-invalid and aria-describedby when error is present', () => {
    const inputEl = fixture.nativeElement.querySelector('input') as HTMLInputElement;
    const formFieldEl = fixture.nativeElement.querySelector('app-form-field') as HTMLElement;

    // Verify host does NOT duplicate the input id
    expect(formFieldEl.getAttribute('id')).toBeNull();
    expect(inputEl.getAttribute('id')).toBe('test-email');

    // Initially no error
    expect(inputEl.getAttribute('aria-invalid')).toBeNull();
    expect(inputEl.getAttribute('aria-describedby')).toBeNull();

    // Set error
    host.error.set('Email is required');
    fixture.detectChanges();

    expect(inputEl.getAttribute('aria-invalid')).toBe('true');
    expect(inputEl.getAttribute('aria-describedby')).toBe('test-email-error');
    expect(inputEl.classList.contains('is-invalid')).toBe(true);

    // Clear error and set hint
    host.error.set('');
    host.hint.set('Enter university email');
    fixture.detectChanges();

    expect(inputEl.getAttribute('aria-invalid')).toBeNull();
    expect(inputEl.getAttribute('aria-describedby')).toBe('test-email-hint');
    expect(inputEl.classList.contains('is-invalid')).toBe(false);
  });
});

