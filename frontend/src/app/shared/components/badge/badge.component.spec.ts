import { ComponentFixture, TestBed } from '@angular/core/testing';
import { BadgeComponent } from './badge.component';

describe('BadgeComponent', () => {
  let component: BadgeComponent;
  let fixture: ComponentFixture<BadgeComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [BadgeComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(BadgeComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should apply variant class for inspection', () => {
    fixture.componentRef.setInput('variant', 'inspection');
    fixture.detectChanges();

    const badgeEl: HTMLElement = fixture.nativeElement.querySelector('.badge');
    expect(badgeEl.classList.contains('badge-inspection')).toBe(true);
    expect(fixture.nativeElement.querySelector('.inspection-icon')).toBeTruthy();
  });

  it('should apply variant class for confirmed and display dot when requested', () => {
    fixture.componentRef.setInput('variant', 'confirmed');
    fixture.componentRef.setInput('showDot', true);
    fixture.detectChanges();

    const badgeEl: HTMLElement = fixture.nativeElement.querySelector('.badge');
    expect(badgeEl.classList.contains('badge-confirmed')).toBe(true);
    expect(fixture.nativeElement.querySelector('.status-dot')).toBeTruthy();
  });
});
