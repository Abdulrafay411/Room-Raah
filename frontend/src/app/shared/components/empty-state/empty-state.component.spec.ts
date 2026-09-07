import { ComponentFixture, TestBed } from '@angular/core/testing';
import { EmptyStateComponent } from './empty-state.component';

describe('EmptyStateComponent', () => {
  let component: EmptyStateComponent;
  let fixture: ComponentFixture<EmptyStateComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [EmptyStateComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(EmptyStateComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should render appropriate title and action for shortlist preset', () => {
    fixture.componentRef.setInput('type', 'shortlist');
    fixture.detectChanges();

    const titleEl = fixture.nativeElement.querySelector('.empty-title');
    expect(titleEl.textContent).toContain('No saved properties yet');
    expect(component.resolvedActionText).toBe('Explore available rooms');
  });

  it('should render appropriate title for inbox preset', () => {
    fixture.componentRef.setInput('type', 'inbox');
    fixture.detectChanges();

    const titleEl = fixture.nativeElement.querySelector('.empty-title');
    expect(titleEl.textContent).toContain('Your inbox is empty');
  });

  it('should render appropriate title for visits preset', () => {
    fixture.componentRef.setInput('type', 'visits');
    fixture.detectChanges();

    const titleEl = fixture.nativeElement.querySelector('.empty-title');
    expect(titleEl.textContent).toContain('No visit requests yet');
  });

  it('should emit actionClicked when primary action button is clicked', () => {
    fixture.componentRef.setInput('type', 'shortlist');
    fixture.detectChanges();

    let emitted = false;
    component.actionClicked.subscribe(() => {
      emitted = true;
    });

    const buttonComponent = fixture.nativeElement.querySelector('app-button');
    expect(buttonComponent).toBeTruthy();
    const nativeBtn = buttonComponent.querySelector('button');
    nativeBtn.click();
    expect(emitted).toBe(true);
  });
});
