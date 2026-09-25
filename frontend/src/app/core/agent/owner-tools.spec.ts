import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { MyListing, PropertyStatus } from '../models/owner.model';
import { VisitRequest } from '../models/engagement.model';
import { OwnerService } from '../services/owner.service';
import { ReferenceService } from '../services/reference.service';
import { OwnerToolbox, effectSentence } from './owner-tools';

function listing(status: PropertyStatus, overrides: Partial<MyListing> = {}): MyListing {
  return {
    id: 5,
    title: 'Bright shared room',
    description: 'Clean.',
    addressLine: 'House 42',
    latitude: 31.5,
    longitude: 74.3,
    roomType: 'Shared',
    genderPolicy: 'Boys',
    totalBeds: 4,
    availableBeds: 2,
    monthlyRent: 18000,
    securityDeposit: 18000,
    utilitiesCharge: 2500,
    messCharge: 8000,
    houseRules: null,
    status,
    hasInspectionBadge: false,
    availabilityConfirmedAt: null,
    areaId: 1,
    areaName: 'Gulberg',
    cityId: 1,
    cityName: 'Lahore',
    facilities: [{ id: 1, name: 'Wi-Fi', iconKey: 'wifi' }],
    photos: [1, 2, 3].map((id) => ({
      id,
      url: '',
      thumbnailUrl: '',
      isPrimary: id === 1,
      sortOrder: id,
      status: 'Live' as const,
      uploadedAt: '',
    })),
    createdAt: '2026-09-01',
    updatedAt: null,
    ...overrides,
  };
}

function visit(status: VisitRequest['status'], preferredAt = '2099-01-01T10:00:00Z'): VisitRequest {
  return {
    id: 21,
    propertyId: 5,
    propertyTitle: 'Bright shared room',
    areaName: 'Gulberg',
    cityName: 'Lahore',
    seekerDisplayName: 'Hamza',
    visitType: 'Physical',
    preferredAt,
    status,
    ownerResponseNote: null,
    respondedAt: null,
    createdAt: '2026-09-20',
  };
}

describe('OwnerToolbox', () => {
  let owner: Record<string, ReturnType<typeof vi.fn>>;
  let current: MyListing;

  function toolbox(): OwnerToolbox {
    TestBed.configureTestingModule({
      providers: [
        { provide: OwnerService, useValue: owner },
        {
          provide: ReferenceService,
          useValue: {
            facilities: () =>
              of([
                { id: 1, name: 'Wi-Fi', iconKey: 'wifi' },
                { id: 2, name: 'Mess', iconKey: 'mess' },
              ]),
          },
        },
        { provide: Router, useValue: { url: '/owner', navigate: vi.fn() } },
      ],
    });
    return TestBed.inject(OwnerToolbox);
  }

  beforeEach(() => {
    current = listing('Published');
    owner = {
      list: vi.fn(() => of({ items: [], totalCount: 0, page: 1, pageSize: 50 })),
      detail: vi.fn(() => of(current)),
      update: vi.fn((_id: number, draft: unknown) =>
        of({ ...current, ...(draft as object), pendingRevision: current.status === 'Published' ? { id: 1 } : null }),
      ),
      setAvailability: vi.fn(() => of(current)),
      submit: vi.fn(() => of({ ...current, status: 'PendingReview' })),
      visits: vi.fn(() => of({ items: [visit('Requested')], totalCount: 1, page: 1, pageSize: 50 })),
      respondToVisit: vi.fn(() => of(visit('Accepted'))),
      completeVisit: vi.fn(() => of(visit('Completed'))),
      requestInspection: vi.fn(() => of({ inspectionRequestedAt: '2026-09-25', hasInspectionBadge: false, lastInspectionAt: null })),
    };
  });

  /** Rule 3: a live listing's change is a revision, and every word about it must say so. */
  it('words a change to a live listing as a review, and reports it as sent - never saved', async () => {
    const prepared = await toolbox().prepare('owner_update_listing', {
      propertyId: 5,
      monthlyRent: 20000,
      addFacilities: ['mess'],
    });
    if (!('pending' in prepared)) throw new Error('expected a confirm');

    expect(prepared.pending.summary).toContain('Monthly rent: Rs. 18,000 → Rs. 20,000');
    expect(prepared.pending.summary).toContain('Add facility: Mess');
    expect(prepared.pending.summary).toContain('for admin review');
    expect(owner['update']).not.toHaveBeenCalled();

    const done = await prepared.pending.run();
    expect(owner['update']).toHaveBeenCalledWith(5, expect.objectContaining({ monthlyRent: 20000, facilityIds: [1, 2] }));
    expect(JSON.stringify(done.result)).toContain('sent for review');
    expect(JSON.stringify(done.result)).not.toMatch(/"done":"saved"/);
  });

  it('says so when an asked-for facility is already there, instead of dropping it silently', async () => {
    const prepared = await toolbox().prepare('owner_update_listing', { propertyId: 5, monthlyRent: 20000, addFacilities: ['wifi'] });
    if (!('pending' in prepared)) throw new Error('expected a confirm');
    expect(prepared.pending.summary).toContain('Wi-Fi is already listed');
  });

  it('saves a draft straight away and says so', async () => {
    current = listing('Draft');
    const prepared = await toolbox().prepare('owner_update_listing', { propertyId: 5, title: 'New title' });
    if (!('pending' in prepared)) throw new Error('expected a confirm');
    expect(prepared.pending.summary).not.toContain('review');
    expect((await prepared.pending.run()).result).toMatchObject({ done: 'saved' });
  });

  it('refuses a change while the review team is reading the listing', async () => {
    current = listing('PendingReview');
    const prepared = await toolbox().prepare('owner_update_listing', { propertyId: 5, monthlyRent: 20000 });
    expect('outcome' in prepared && prepared.outcome.result).toMatchObject({ error: expect.stringContaining('review team') });
  });

  it('does not ask to confirm a change that changes nothing', async () => {
    const prepared = await toolbox().prepare('owner_update_listing', { propertyId: 5, monthlyRent: 18000 });
    expect('outcome' in prepared && prepared.outcome.result).toMatchObject({ error: expect.stringContaining('Nothing would change') });
  });

  it('keeps free beds within the total, and confirms them as their own action', async () => {
    const tools = toolbox();
    const tooMany = await tools.prepare('owner_confirm_availability', { propertyId: 5, availableBeds: 9 });
    expect('outcome' in tooMany).toBe(true);

    const prepared = await tools.prepare('owner_confirm_availability', { propertyId: 5, availableBeds: 0 });
    if (!('pending' in prepared)) throw new Error('expected a confirm');
    expect(prepared.pending.summary).toBe('Confirm 0 of 4 beds free at "Bright shared room"?');
    await prepared.pending.run();
    expect(owner['setAvailability']).toHaveBeenCalledWith(5, 0);
  });

  it('says why a listing cannot be submitted before asking anyone to confirm', async () => {
    current = listing('Draft', { photos: [] });
    const prepared = await toolbox().prepare('owner_submit_listing', { propertyId: 5 });
    expect('outcome' in prepared && prepared.outcome.result).toMatchObject({ error: expect.stringContaining('3 more photographs') });
    expect(owner['submit']).not.toHaveBeenCalled();
  });

  /** Rule 6: a decline carries a note. */
  it('will not decline a visit without a note for the seeker', async () => {
    const prepared = await toolbox().prepare('owner_respond_to_visit', { visitId: 21, decision: 'Declined' });
    expect('outcome' in prepared && prepared.outcome.result).toMatchObject({ error: expect.stringContaining('note') });
  });

  it('accepts a requested visit on confirm, naming who and when', async () => {
    const prepared = await toolbox().prepare('owner_respond_to_visit', { visitId: 21, decision: 'Accepted' });
    if (!('pending' in prepared)) throw new Error('expected a confirm');
    expect(prepared.pending.summary).toMatch(/^Accept Hamza's in-person visit to "Bright shared room"/);
    await prepared.pending.run();
    expect(owner['respondToVisit']).toHaveBeenCalledWith(21, 'Accepted', undefined);
  });

  it('will not answer a visit that is no longer waiting for one', async () => {
    owner['visits'] = vi.fn(() => of({ items: [visit('Cancelled')], totalCount: 1, page: 1, pageSize: 50 }));
    const prepared = await toolbox().prepare('owner_respond_to_visit', { visitId: 21, decision: 'Accepted' });
    expect('outcome' in prepared && prepared.outcome.result).toMatchObject({ error: expect.stringContaining('Cancelled') });
  });

  it('tells the owner what a save will do before they change anything', () => {
    expect(effectSentence(listing('Published'))).toContain('admin for review');
    expect(effectSentence(listing('Draft'))).toContain('saved straight away');
  });
});
