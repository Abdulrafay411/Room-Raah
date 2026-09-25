import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { PropertyCard } from '../models/catalog.model';
import { CompareStore } from '../services/compare.store';
import { EngagementService } from '../services/engagement.service';
import { GeolocationService } from '../services/geolocation.service';
import { MessagingService } from '../services/messaging.service';
import { PropertyService } from '../services/property.service';
import { ReferenceService } from '../services/reference.service';
import { OwnerService } from '../services/owner.service';
import { AdminService } from '../services/admin.service';
import { AgentToolbox } from './agent-tools';

function card(id: number, monthlyRent: number, latitude = 31.5, longitude = 74.3): PropertyCard {
  return {
    id,
    title: `Hostel ${id}`,
    areaId: 1,
    areaName: 'Gulberg',
    cityId: 1,
    cityName: 'Lahore',
    roomType: 'Shared',
    genderPolicy: 'Boys',
    totalBeds: 8,
    availableBeds: 3,
    monthlyRent,
    securityDeposit: 10000,
    utilitiesCharge: 1500,
    messCharge: 0,
    latitude,
    longitude,
    primaryThumbnailUrl: null,
    photoCount: 2,
    hasInspectionBadge: true,
    availabilityConfirmedAt: null,
    distanceKm: null,
  };
}

describe('AgentToolbox', () => {
  let properties: Record<string, ReturnType<typeof vi.fn>>;
  let engagement: Record<string, ReturnType<typeof vi.fn>>;
  let router: { url: string; navigate: ReturnType<typeof vi.fn> };
  let auth: { authenticated: boolean; verified: boolean; role: string | null };
  let position: { lat: number; lng: number } | null;

  function toolbox(): AgentToolbox {
    TestBed.configureTestingModule({
      providers: [
        { provide: PropertyService, useValue: properties },
        {
          provide: ReferenceService,
          useValue: {
            facilities: () => of([{ id: 1, name: 'WiFi', iconKey: 'wifi' }]),
            cities: () => of([{ id: 1, name: 'Lahore' }, { id: 3, name: 'Islamabad' }]),
            suggest: (q: string) =>
              of(
                /lahore/i.test(q)
                  ? [{ type: 'City', id: 1, name: 'Lahore' }]
                  : /^nust$/i.test(q)
                    ? [{ type: 'Landmark', id: 7, name: 'NUST', city: 'Islamabad' }]
                    : /^punjab$/i.test(q)
                      ? [{ type: 'Landmark', id: 4, name: 'University of the Punjab', city: 'Lahore' }]
                    : /^univ/i.test(q)
                      ? [{ type: 'Landmark', id: 2, name: 'University of Karachi', city: 'Karachi' }]
                      : [],
              ),
          },
        },
        { provide: EngagementService, useValue: engagement },
        { provide: MessagingService, useValue: {} },
        { provide: OwnerService, useValue: {} },
        { provide: AdminService, useValue: {} },
        {
          provide: GeolocationService,
          useValue: {
            hasPosition: () => position !== null,
            state: () => (position ? 'granted' : 'idle'),
            request: vi.fn(),
            distanceTo: (lat: number, lng: number) =>
              position ? Math.abs(lat - position.lat) * 100 + Math.abs(lng - position.lng) * 100 : null,
          },
        },
        { provide: Router, useValue: router },
        {
          provide: AuthService,
          useValue: {
            isAuthenticated: () => auth.authenticated,
            isEmailVerified: () => auth.verified,
            userRole: () => auth.role,
          },
        },
        CompareStore,
      ],
    });
    return TestBed.inject(AgentToolbox);
  }

  beforeEach(() => {
    localStorage.clear();
    position = null;
    auth = { authenticated: true, verified: true, role: 'Seeker' };
    router = { url: '/search', navigate: vi.fn() };
    properties = {
      search: vi.fn(() => of({ items: [card(1, 30000), card(2, 18000)], totalCount: 2, page: 1, pageSize: 50 })),
      reviews: vi.fn(() => of({ items: [], totalCount: 0, page: 1, pageSize: 1, averageRating: 0 })),
      detail: vi.fn((id: number) => of({ ...card(id, 20000), title: 'Al-Madina' })),
    };
    engagement = {
      saved: vi.fn(() => of({ items: [{ id: 3 }], totalCount: 1, page: 1, pageSize: 50 })),
      save: vi.fn(() => of({})), hasOpenVisit: vi.fn(() => false), requestVisit: vi.fn(() => of({})) };
  });

  it('turns the model\'s words into real filters and ranks cheapest first without a position', async () => {
    const prepared = await toolbox().prepare('search_hostels', {
      place: 'Lahore',
      maxRent: '25k'.replace('k', '000'),
      genderPolicy: 'boys',
      facilities: ['wi-fi'],
    });

    expect(properties['search']).toHaveBeenCalledWith(
      expect.objectContaining({ cityId: 1, maxRent: 25000, genderPolicy: 'Boys', facilityIds: [1] }),
    );
    const outcome = 'outcome' in prepared ? prepared.outcome : null;
    expect(outcome?.cards?.map((c) => c.id)).toEqual([2, 1]);
    expect(outcome?.result).toMatchObject({ exactMatch: true, shown: 2 });
  });

  it('ranks nearest first from the person when their position is known', async () => {
    position = { lat: 31.5, lng: 74.3 };
    properties['search'] = vi.fn(() =>
      of({ items: [card(1, 10000, 31.6, 74.3), card(2, 30000, 31.51, 74.3)], totalCount: 2, page: 1, pageSize: 50 }),
    );
    const prepared = await toolbox().prepare('search_hostels', { nearMe: true, genderPolicy: 'Any' });
    const outcome = 'outcome' in prepared ? prepared.outcome : null;
    expect(outcome?.cards?.map((c) => c.id)).toEqual([2, 1]);
    expect(outcome?.cards?.[0].distanceLabel).toMatch(/km from you \(straight-line\)/);
  });

  it('loosens the search and says so when nothing matched exactly', async () => {
    properties['search'] = vi
      .fn()
      .mockReturnValueOnce(of({ items: [], totalCount: 0, page: 1, pageSize: 50 }))
      .mockReturnValueOnce(of({ items: [card(3, 27000)], totalCount: 1, page: 1, pageSize: 50 }));
    const prepared = await toolbox().prepare('search_hostels', { maxRent: 25000, facilities: ['WiFi'], genderPolicy: 'Any' });

    expect(properties['search']).toHaveBeenLastCalledWith(
      expect.objectContaining({ maxRent: 30000, facilityIds: undefined }),
    );
    const outcome = 'outcome' in prepared ? prepared.outcome : null;
    expect(outcome?.result).toMatchObject({ exactMatch: false });
  });

  it('falls back to the city a place mentions, however it is spelt, and says so', async () => {
    const prepared = await toolbox().prepare('search_hostels', { place: 'FAST University islamabaad', genderPolicy: 'Boys' });
    expect(properties['search']).toHaveBeenCalledWith(expect.objectContaining({ cityId: 3 }));
    const notes = ('outcome' in prepared && (prepared.outcome.result as { notes: string[] }).notes) || [];
    expect(notes.join(' ')).toContain('does not know "FAST University islamabaad"');
  });

  it('finds a landmark named by one word of what the model sent', async () => {
    await toolbox().prepare('search_hostels', { place: 'NUST Islamabad', genderPolicy: 'Any' });
    expect(properties['search']).toHaveBeenCalledWith(expect.objectContaining({ landmarkId: 7, sort: 'Distance' }));
  });

  it('finds "University of the Punjab" from "Punjab University"', async () => {
    await toolbox().prepare('search_hostels', { place: 'Punjab University', genderPolicy: 'Any' });
    expect(properties['search']).toHaveBeenCalledWith(expect.objectContaining({ landmarkId: 4 }));
  });

  it('treats "near" as near, and says how far the nearest one is when nothing is', async () => {
    position = { lat: 31.5, lng: 74.3 };
    properties['search'] = vi.fn(() =>
      of({ items: [card(9, 20000, 33.6, 73.0)], totalCount: 1, page: 1, pageSize: 50 }),
    );
    const prepared = await toolbox().prepare('search_hostels', { nearMe: true, genderPolicy: 'Any' });
    const outcome = 'outcome' in prepared ? prepared.outcome : null;
    expect(outcome?.cards ?? []).toHaveLength(0);
    expect((outcome?.result as { notes: string[] }).notes.join(' ')).toMatch(/Nothing is listed within 30 km.*Hostel 9/);
  });

  it('asks rather than guessing when it does not know the place', async () => {
    const prepared = await toolbox().prepare('search_hostels', { place: 'Atlantis', genderPolicy: 'Any' });
    expect(properties['search']).not.toHaveBeenCalled();
    expect('outcome' in prepared && prepared.outcome.result).toMatchObject({ error: expect.stringContaining('Atlantis') });
  });

  describe('who the room is for', () => {
    it('asks before searching when nobody has said', async () => {
      const prepared = await toolbox().prepare('search_hostels', { place: 'Lahore' });
      expect('outcome' in prepared && prepared.outcome.ask).toBe('gender');
      expect(properties['search']).not.toHaveBeenCalled();
    });

    it('does not ask twice - answered "any", it searches everyone', async () => {
      await toolbox().prepare('search_hostels', { place: 'Lahore' }, { genderAsked: true, genderHint: 'Any' });
      expect(properties['search']).toHaveBeenCalledWith(expect.objectContaining({ genderPolicy: undefined }));
    });

    it('uses what the person said when the model left it out of the call', async () => {
      await toolbox().prepare('search_hostels', { place: 'Lahore' }, { genderHint: 'Girls' });
      expect(properties['search']).toHaveBeenCalledWith(expect.objectContaining({ genderPolicy: 'Girls' }));
    });

    it('lets an "Any" from the model through without asking', async () => {
      await toolbox().prepare('search_hostels', { place: 'Lahore', genderPolicy: 'Any' });
      expect(properties['search']).toHaveBeenCalledWith(expect.objectContaining({ genderPolicy: undefined }));
    });
  });

  it('holds a save for confirmation, worded from the real listing', async () => {
    const prepared = await toolbox().prepare('save_hostel', { propertyId: 7 });
    expect('pending' in prepared).toBe(true);
    if (!('pending' in prepared)) return;

    expect(prepared.pending.summary).toBe('Save "Al-Madina" to your shortlist?');
    expect(engagement['save']).not.toHaveBeenCalled();
    const done = await prepared.pending.run();
    expect(engagement['save']).toHaveBeenCalledWith(7);
    expect(done.result).toMatchObject({ ok: true });
  });

  it('does not ask to save what is already saved - the API would only refuse it', async () => {
    const prepared = await toolbox().prepare('save_hostel', { propertyId: 3 });
    expect('outcome' in prepared && prepared.outcome.result).toMatchObject({ done: false });
    expect(engagement['save']).not.toHaveBeenCalled();
  });

  it('sends a signed-out visitor to sign in instead of trying', async () => {
    auth = { authenticated: false, verified: false, role: null };
    const prepared = await toolbox().prepare('save_hostel', { propertyId: 7 });
    expect('outcome' in prepared && prepared.outcome.links?.[0].label).toBe('Sign in');
  });

  /** Rule 3: an unverified account goes to /verify, never to a generic error. */
  it('routes an unverified account to /verify', async () => {
    auth = { authenticated: true, verified: false, role: 'Seeker' };
    await toolbox().prepare('request_visit', { propertyId: 7, visitType: 'Physical', preferredAt: '2099-01-01T10:00' });
    expect(router.navigate).toHaveBeenCalledWith(['/verify']);
  });

  it('refuses a visit time in the past before asking anyone to confirm it', async () => {
    const prepared = await toolbox().prepare('request_visit', {
      propertyId: 7,
      visitType: 'Physical',
      preferredAt: '2020-01-01T10:00',
    });
    expect('outcome' in prepared && prepared.outcome.result).toMatchObject({ error: 'Choose a time in the future.' });
  });

  it('does not offer an owner the seeker-only tools', () => {
    auth = { authenticated: true, verified: true, role: 'Owner' };
    const names = toolbox().definitions().map((d) => d.function.name);
    expect(names).toContain('search_hostels');
    expect(names).not.toContain('save_hostel');
    expect(names).toContain('owner_update_listing');
    expect(names).toContain('reply_in_conversation');
  });

  it('offers an admin the admin tools and nothing a seeker or owner does', () => {
    auth = { authenticated: true, verified: true, role: 'Admin' };
    const names = toolbox().definitions().map((d) => d.function.name);
    expect(names).toContain('admin_decide_listing');
    expect(names).toContain('search_hostels');
    expect(names.some((n) => n.startsWith('owner_') || n === 'save_hostel' || n === 'my_conversations')).toBe(false);
  });

  it('does not offer a seeker the owner tools, and refuses them if called anyway', async () => {
    const names = toolbox().definitions().map((d) => d.function.name);
    expect(names).not.toContain('owner_update_listing');
    expect(names.filter((n) => n.startsWith('owner_'))).toEqual([]);
    const prepared = await TestBed.inject(AgentToolbox).prepare('owner_listings', {});
    expect('outcome' in prepared && prepared.outcome.result).toMatchObject({ error: 'Only an owner account can do this.' });
  });

  it('opens a page the way a link would', async () => {
    await toolbox().prepare('open_page', { page: 'hostel', propertyId: 12 });
    expect(router.navigate).toHaveBeenCalledWith(['/property', 12], { queryParams: undefined });
  });
});
