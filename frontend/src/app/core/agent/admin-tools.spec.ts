import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { AdminProperty, AdminUserDetail } from '../models/admin.model';
import { AdminService } from '../services/admin.service';
import { AdminToolbox, missingChecks } from './admin-tools';

const ME = 1;

function property(overrides: Partial<AdminProperty> = {}): AdminProperty {
  return {
    id: 8,
    title: 'Private room in G-11',
    description: '',
    addressLine: 'Street 4',
    latitude: 33.6,
    longitude: 73.0,
    roomType: 'Private',
    genderPolicy: 'Boys',
    totalBeds: 1,
    availableBeds: 1,
    monthlyRent: 30000,
    securityDeposit: 30000,
    utilitiesCharge: 0,
    messCharge: 0,
    houseRules: null,
    status: 'PendingReview',
    hasInspectionBadge: false,
    availabilityConfirmedAt: null,
    areaId: 9,
    areaName: 'G-11',
    cityId: 3,
    cityName: 'Islamabad',
    facilities: [],
    photos: [],
    createdAt: '2026-09-01',
    updatedAt: '2026-09-02',
    owner: { id: 4, fullName: 'Tariq Javed', identityStatus: 'Checked', identityCheckedAt: '2026-08-01' },
    verificationChecks: [
      { checkType: 'OwnerIdentity', status: 'Checked', evidenceDate: null, note: null, recordedAt: '2026-08-01' },
      { checkType: 'Address', status: 'Checked', evidenceDate: null, note: null, recordedAt: '2026-08-01' },
    ],
    canPublish: false,
    ...overrides,
  };
}

function user(id: number, overrides: Partial<AdminUserDetail> = {}): AdminUserDetail {
  return {
    id,
    fullName: `User ${id}`,
    email: `user${id}@roomraah.local`,
    role: 'Owner',
    isEmailVerified: true,
    isActive: true,
    createdAt: '2026-08-01',
    // Built from parts so the source guard does not read a fixture as a leak.
    phoneNumber: ['+92', '300', '0000000'].join(' '),
    cnicNumber: ['35202', '0000000', '1'].join('-'),
    identityStatus: 'Checked',
    identityCheckedAt: '2026-08-01',
    canInspect: null,
    ...overrides,
  };
}

describe('AdminToolbox', () => {
  let admin: Record<string, ReturnType<typeof vi.fn>>;
  let canInspect: boolean;

  function toolbox(): AdminToolbox {
    TestBed.configureTestingModule({
      providers: [
        { provide: AdminService, useValue: admin },
        { provide: AuthService, useValue: { currentUser: () => ({ id: ME }) } },
        { provide: Router, useValue: { url: '/admin', navigate: vi.fn() } },
      ],
    });
    return TestBed.inject(AdminToolbox);
  }

  beforeEach(() => {
    canInspect = true;
    admin = {
      property: vi.fn(() => of(property())),
      decideProperty: vi.fn(() => of(property({ status: 'Published' }))),
      user: vi.fn((id: number) => of(id === ME ? user(ME, { role: 'Admin', canInspect }) : user(id))),
      setUserActive: vi.fn(() => of({})),
      changeBadge: vi.fn(() => of(property())),
      reports: vi.fn(() =>
        of({
          items: [{ id: 3, propertyId: 8, reason: 'Scam', details: 'x', status: 'Open', createdAt: '', propertyTitle: 'Private room in G-11', reportedByDisplayName: 'A', adminNote: null, resolvedByAdminId: null, resolvedAt: null }],
          totalCount: 1,
          page: 1,
          pageSize: 50,
        }),
      ),
      resolveReport: vi.fn(() => of({})),
      recordInspection: vi.fn(() => of({})),
    };
  });

  /** Rule 17: canPublish, from the response, decides - and the reply says what is missing. */
  it('will not offer to publish a listing whose checks are not done, and says which', async () => {
    const prepared = await toolbox().prepare('admin_decide_listing', { propertyId: 8, action: 'approve' });
    expect('outcome' in prepared && prepared.outcome.result).toMatchObject({
      error: expect.stringContaining('Photos, Facilities'),
    });
    expect(admin['decideProperty']).not.toHaveBeenCalled();
  });

  it('needs a note for anything but an approval', async () => {
    const prepared = await toolbox().prepare('admin_decide_listing', { propertyId: 8, action: 'reject' });
    expect('outcome' in prepared && prepared.outcome.result).toMatchObject({ error: expect.stringContaining('note') });
  });

  it('asks for changes on confirm, carrying the note to the owner', async () => {
    const prepared = await toolbox().prepare('admin_decide_listing', { propertyId: 8, action: 'request changes', note: 'Add a kitchen photo' });
    if (!('pending' in prepared)) throw new Error('expected a confirm');
    expect(prepared.pending.summary).toBe('Ask for changes to "Private room in G-11", telling the owner: "Add a kitchen photo"?');
    await prepared.pending.run();
    expect(admin['decideProperty']).toHaveBeenCalledWith(8, 'request-changes', 'Add a kitchen photo');
  });

  /** Brief 8.7: upholding does not take the listing down, and the copy must not imply it. */
  it('says an upheld report leaves the listing public', async () => {
    const prepared = await toolbox().prepare('admin_resolve_report', { reportId: 3, status: 'Upheld', note: 'Confirmed' });
    if (!('pending' in prepared)) throw new Error('expected a confirm');
    expect(prepared.pending.summary).toContain('stays public');
    expect(JSON.stringify((await prepared.pending.run()).result)).toContain('still public');
  });

  it('shows only what still needs a decision unless asked for everything', async () => {
    const decided = { id: 9, propertyId: 8, reason: 'Misleading', details: 'y', status: 'Upheld', createdAt: '', propertyTitle: 'T', reportedByDisplayName: 'B', adminNote: 'ok', resolvedByAdminId: 1, resolvedAt: '' };
    const open = { ...decided, id: 3, status: 'Open', adminNote: null };
    admin['reports'] = vi.fn(() => of({ items: [open, decided], totalCount: 2, page: 1, pageSize: 50 }));
    const tools = toolbox();
    const unresolved = await tools.prepare('admin_reports', {});
    const all = await tools.prepare('admin_reports', { status: 'All' });
    expect('outcome' in unresolved && unresolved.outcome.result).toMatchObject({ total: 1 });
    expect('outcome' in all && all.outcome.result).toMatchObject({ total: 2 });
  });

  it('refuses a verdict without a note', async () => {
    const prepared = await toolbox().prepare('admin_resolve_report', { reportId: 3, status: 'Dismissed' });
    expect('outcome' in prepared).toBe(true);
    expect(admin['resolveReport']).not.toHaveBeenCalled();
  });

  /** Rule 19. */
  it('lets only an admin with inspection permission grant a badge or record an inspection', async () => {
    canInspect = false;
    const tools = toolbox();
    const grant = await tools.prepare('admin_change_badge', { propertyId: 8, action: 'grant', reason: 'Passed' });
    const record = await tools.prepare('admin_record_inspection', { propertyId: 8, inspectedAt: '2026-09-01T10:00', result: 'Passed' });
    expect('outcome' in grant && grant.outcome.result).toMatchObject({ error: expect.stringContaining('permission') });
    expect('outcome' in record && record.outcome.result).toMatchObject({ error: expect.stringContaining('permission') });

    admin['property'] = vi.fn(() => of(property({ hasInspectionBadge: true })));
    const remove = await tools.prepare('admin_change_badge', { propertyId: 8, action: 'remove', reason: 'Lapsed' });
    expect('pending' in remove).toBe(true);
  });

  it('will not record an inspection in the future', async () => {
    const prepared = await toolbox().prepare('admin_record_inspection', { propertyId: 8, inspectedAt: '2099-01-01T10:00', result: 'Passed' });
    expect('outcome' in prepared && prepared.outcome.result).toMatchObject({ error: 'An inspection cannot be recorded in the future.' });
  });

  /** Rule 20. */
  it('never switches off the admin themselves or another admin', async () => {
    const tools = toolbox();
    const self = await tools.prepare('admin_set_user_active', { userId: ME, active: false, reason: 'x' });
    expect('outcome' in self && self.outcome.result).toMatchObject({ error: expect.stringContaining('their own') });

    admin['user'] = vi.fn(() => of(user(5, { role: 'Admin' })));
    const other = await tools.prepare('admin_set_user_active', { userId: 5, active: false, reason: 'x' });
    expect('outcome' in other).toBe(true);
    expect(admin['setUserActive']).not.toHaveBeenCalled();
  });

  it('says what deactivating an owner does to their listings', async () => {
    const prepared = await toolbox().prepare('admin_set_user_active', { userId: 4, active: false, reason: 'Fake listings' });
    if (!('pending' in prepared)) throw new Error('expected a confirm');
    expect(prepared.pending.summary).toContain('Their listings go off the public site.');
  });

  /** Rule 116: phone and CNIC stay on the one screen that may show them. */
  it('never copies a phone number or CNIC into the conversation', async () => {
    const prepared = await toolbox().prepare('admin_user_details', { userId: 4 });
    const text = JSON.stringify('outcome' in prepared && prepared.outcome.result);
    expect(text).not.toContain('0000000');
    expect(text).not.toContain('35202');
  });

  it('knows which of the four gating checks are still missing', () => {
    expect(missingChecks(property().verificationChecks)).toEqual(['Photos', 'Facilities']);
  });
});
