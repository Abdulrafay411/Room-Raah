import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { VisitRequest } from '../../../core/models/engagement.model';
import { EngagementService } from '../../../core/services/engagement.service';
import { PropertyService } from '../../../core/services/property.service';
import { VisitsComponent } from './visits.component';

function visit(id: number, propertyId: number, overrides: Partial<VisitRequest>): VisitRequest {
  return {
    id,
    propertyId,
    propertyTitle: `Room ${propertyId}`,
    areaName: 'Gulberg',
    cityName: 'Lahore',
    seekerDisplayName: 'Hamza Iqbal',
    visitType: 'Physical',
    preferredAt: '2099-01-01T10:00:00Z',
    status: 'Requested',
    ownerResponseNote: null,
    respondedAt: null,
    createdAt: '2026-09-01T10:00:00Z',
    ...overrides,
  };
}

describe('VisitsComponent - directions', () => {
  let detail: ReturnType<typeof vi.fn>;

  function render(visits: VisitRequest[]): HTMLElement {
    TestBed.configureTestingModule({
      imports: [VisitsComponent],
      providers: [
        provideRouter([]),
        {
          provide: EngagementService,
          useValue: { myVisits: () => of({ items: visits, totalCount: visits.length, page: 1, pageSize: 50 }) },
        },
        { provide: PropertyService, useValue: { detail } },
      ],
    });
    const fixture = TestBed.createComponent(VisitsComponent);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  const links = (root: HTMLElement) =>
    Array.from(root.querySelectorAll<HTMLAnchorElement>('[data-smoke=visit-directions]'));

  beforeEach(() => {
    detail = vi.fn((id: number) => of({ id, latitude: 31.52, longitude: 74.35 }));
  });

  it('shows Get directions on an accepted in-person visit, pointing at the listing', () => {
    const root = render([visit(1, 4, { status: 'Accepted' })]);
    const [link] = links(root);
    expect(link.getAttribute('href')).toBe('https://www.google.com/maps/dir/?api=1&destination=31.52,74.35');
    expect(link.getAttribute('target')).toBe('_blank');
    expect(link.getAttribute('rel')).toContain('noopener');
  });

  it('shows nothing on requested, completed or video visits, and does not fetch for them', () => {
    const root = render([
      visit(1, 4, { status: 'Requested' }),
      visit(2, 5, { status: 'Completed' }),
      visit(3, 6, { status: 'Accepted', visitType: 'Video' }),
    ]);
    expect(links(root)).toHaveLength(0);
    expect(detail).not.toHaveBeenCalled();
  });

  it('fetches each listing once however many accepted visits point at it', () => {
    render([visit(1, 4, { status: 'Accepted' }), visit(2, 4, { status: 'Accepted' })]);
    expect(detail).toHaveBeenCalledTimes(1);
  });

  /** A listing taken off the site is a 404; it costs its own link, not the page. */
  it('leaves out only the link whose listing could not be read', () => {
    detail = vi.fn((id: number) =>
      id === 4 ? throwError(() => new Error('404')) : of({ id, latitude: 33.6, longitude: 73.0 }),
    );
    const root = render([visit(1, 4, { status: 'Accepted' }), visit(2, 9, { status: 'Accepted' })]);
    expect(links(root).map((a) => a.getAttribute('href'))).toEqual([
      'https://www.google.com/maps/dir/?api=1&destination=33.6,73',
    ]);
    expect(root.querySelector('[data-smoke=visits-list]')).not.toBeNull();
  });
});
