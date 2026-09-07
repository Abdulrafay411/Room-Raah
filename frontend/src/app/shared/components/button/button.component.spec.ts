import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ButtonComponent } from './button.component';

describe('ButtonComponent', () => {
  let component: ButtonComponent;
  let fixture: ComponentFixture<ButtonComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ButtonComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(ButtonComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should apply primary variant and medium size by default', () => {
    const buttonEl: HTMLButtonElement = fixture.nativeElement.querySelector('button');
    expect(buttonEl.classList.contains('btn-primary')).toBe(true);
    expect(buttonEl.classList.contains('btn-md')).toBe(true);
  });

  it('should emit clicked event when clicked and not disabled or loading', () => {
    let emitted = false;
    component.clicked.subscribe(() => {
      emitted = true;
    });

    const buttonEl: HTMLButtonElement = fixture.nativeElement.querySelector('button');
    buttonEl.click();
    expect(emitted).toBe(true);
  });

  it('should not emit clicked event when disabled', () => {
    fixture.componentRef.setInput('disabled', true);
    fixture.detectChanges();

    let emitted = false;
    component.clicked.subscribe(() => {
      emitted = true;
    });

    const buttonEl: HTMLButtonElement = fixture.nativeElement.querySelector('button');
    buttonEl.click();
    expect(emitted).toBe(false);
    expect(buttonEl.disabled).toBe(true);
    expect(buttonEl.getAttribute('aria-disabled')).toBe('true');
  });

  it('should show spinner and set aria-busy when loading', () => {
    fixture.componentRef.setInput('loading', true);
    fixture.detectChanges();

    const buttonEl: HTMLButtonElement = fixture.nativeElement.querySelector('button');
    expect(buttonEl.classList.contains('btn-loading')).toBe(true);
    expect(buttonEl.getAttribute('aria-busy')).toBe('true');
    expect(fixture.nativeElement.querySelector('app-spinner')).toBeTruthy();
  });

  it('should support aria-pressed for toggle buttons', () => {
    fixture.componentRef.setInput('pressed', true);
    fixture.detectChanges();

    const buttonEl: HTMLButtonElement = fixture.nativeElement.querySelector('button');
    expect(buttonEl.getAttribute('aria-pressed')).toBe('true');
  });

  it('should forward ariaLabel to button attribute', () => {
    fixture.componentRef.setInput('ariaLabel', 'Save listing to shortlist');
    fixture.componentRef.setInput('isIconOnly', true);
    fixture.detectChanges();

    const buttonEl: HTMLButtonElement = fixture.nativeElement.querySelector('button');
    expect(buttonEl.getAttribute('aria-label')).toBe('Save listing to shortlist');
    expect(buttonEl.classList.contains('btn-icon-only')).toBe(true);
  });
});
