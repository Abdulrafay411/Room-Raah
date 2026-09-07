import { ComponentFixture, TestBed } from '@angular/core/testing';
import { DialogComponent } from './dialog.component';

describe('DialogComponent', () => {
  let component: DialogComponent;
  let fixture: ComponentFixture<DialogComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DialogComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(DialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should not render dialog panel when isOpen is false', () => {
    fixture.componentRef.setInput('isOpen', false);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.dialog-panel')).toBeNull();
  });

  it('should render dialog with role="dialog" and aria-modal="true" when isOpen is true', () => {
    fixture.componentRef.setInput('isOpen', true);
    fixture.componentRef.setInput('title', 'Test Dialog Title');
    fixture.detectChanges();

    const panelEl: HTMLElement = fixture.nativeElement.querySelector('.dialog-panel');
    expect(panelEl).toBeTruthy();
    expect(panelEl.getAttribute('role')).toBe('dialog');
    expect(panelEl.getAttribute('aria-modal')).toBe('true');
  });

  it('should close when Escape key is pressed', () => {
    fixture.componentRef.setInput('isOpen', true);
    fixture.detectChanges();

    let closed = false;
    component.closed.subscribe(() => {
      closed = true;
    });

    const escapeEvent = new KeyboardEvent('keydown', { key: 'Escape' });
    component.onEscapeKey(escapeEvent);

    expect(closed).toBe(true);
    expect(component.isOpen).toBe(false);
  });

  it('should close when backdrop is clicked if closeOnBackdropClick is true', () => {
    fixture.componentRef.setInput('isOpen', true);
    fixture.componentRef.setInput('closeOnBackdropClick', true);
    fixture.detectChanges();

    let closed = false;
    component.closed.subscribe(() => {
      closed = true;
    });

    const backdropEl = document.createElement('div');
    const clickEvent = {
      target: backdropEl,
      currentTarget: backdropEl,
    } as unknown as MouseEvent;

    component.onBackdropClick(clickEvent);
    expect(closed).toBe(true);
  });

  it('should show loading spinner when loading is true', () => {
    fixture.componentRef.setInput('isOpen', true);
    fixture.componentRef.setInput('loading', true);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.dialog-loading-overlay')).toBeTruthy();
  });
});
