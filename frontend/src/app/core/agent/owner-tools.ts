import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ListingDraft, MyListing, canSubmit, livePhotoCount, saveEffect, statusLabel, submitProblem, toDraft } from '../models/owner.model';
import { ROOM_TYPES, GENDER_POLICIES } from '../models/catalog.model';
import { VisitRequest, canComplete } from '../models/engagement.model';
import { OwnerService } from '../services/owner.service';
import { ReferenceService } from '../services/reference.service';
import { ToolDefinition, ToolOutcome } from './agent.model';
import { asInt, asText, day, matchFacilities, rupees, toGenderPolicy, toRoomType } from './agent.util';
import { Args, Prepared, required, runWrite, toolFailure, when } from './tool-support';

function tool(name: string, description: string, properties: Args = {}, required: string[] = []): ToolDefinition {
  return { type: 'function', function: { name, description, parameters: { type: 'object', properties, required } } };
}

const listingId = { type: 'integer', description: "The owner's listing id, from owner_listings" };

/** The fields an owner may change by chat. Location and photographs need the form's map and file picker. */
const EDITABLE = {
  title: { type: 'string' },
  description: { type: 'string' },
  addressLine: { type: 'string' },
  monthlyRent: { type: 'integer', description: 'Rupees per month' },
  securityDeposit: { type: 'integer' },
  utilitiesCharge: { type: 'integer' },
  messCharge: { type: 'integer' },
  totalBeds: { type: 'integer' },
  availableBeds: { type: 'integer' },
  roomType: { type: 'string', enum: ROOM_TYPES },
  genderPolicy: { type: 'string', enum: GENDER_POLICIES },
  houseRules: { type: 'string' },
  addFacilities: { type: 'array', items: { type: 'string' }, description: 'Facility names to add' },
  removeFacilities: { type: 'array', items: { type: 'string' }, description: 'Facility names to remove' },
};

export const OWNER_TOOL_DEFINITIONS: ToolDefinition[] = [
  tool('owner_listings', "The owner's own listings with status, rent, beds, photos, inspection badge and when availability was last confirmed."),
  tool('owner_listing_details', 'Everything about one of their listings, and what saving a change to it would do.', { propertyId: listingId }, ['propertyId']),
  tool(
    'owner_update_listing',
    'Change fields of one of their listings. On a published listing this sends the change for admin review and the live listing stays as it is until approved. Location and photographs are changed on the listing form, not here.',
    { propertyId: listingId, ...EDITABLE },
    ['propertyId'],
  ),
  tool(
    'owner_confirm_availability',
    'Confirm how many beds are free right now. This is its own fast review, separate from editing, and re-confirming the same number is allowed.',
    { propertyId: listingId, availableBeds: { type: 'integer' } },
    ['propertyId', 'availableBeds'],
  ),
  tool('owner_submit_listing', 'Send a draft, or a listing they were asked to change, to the review team. Needs at least 3 photographs.', { propertyId: listingId }, ['propertyId']),
  tool('owner_request_inspection', 'Ask RoomRaah to inspect a listing for the inspection badge. Each listing gets one request.', { propertyId: listingId }, ['propertyId']),
  tool('owner_visit_requests', 'Visit requests seekers have made on their listings, with status and what can be done with each.'),
  tool(
    'owner_respond_to_visit',
    'Accept or decline a visit request that is still Requested. A decline must carry a note, which the seeker sees.',
    {
      visitId: { type: 'integer', description: 'From owner_visit_requests' },
      decision: { type: 'string', enum: ['Accepted', 'Declined'] },
      note: { type: 'string', description: 'Required when declining' },
    },
    ['visitId', 'decision'],
  ),
  tool('owner_mark_visit_completed', 'Mark an accepted visit as done, once its time has passed.', { visitId: { type: 'integer' } }, ['visitId']),
];

export const OWNER_TOOL_NAMES = new Set(OWNER_TOOL_DEFINITIONS.map((t) => t.function.name));

/**
 * The owner's half of the assistant: their listings and the visit requests on them.
 *
 * The rule everything here is built around is the brief's rule 3. A published listing is
 * never edited in place - a change becomes a revision an admin approves - so the confirm
 * card says that before the owner agrees, and the result says "sent for review" whenever the
 * API answered with a pending revision, never "saved".
 */
@Injectable({ providedIn: 'root' })
export class OwnerToolbox {
  private readonly owner = inject(OwnerService);
  private readonly reference = inject(ReferenceService);
  private readonly router = inject(Router);

  async prepare(name: string, args: Args): Promise<Prepared> {
    try {
      switch (name) {
        case 'owner_listings':
          return { outcome: await this.listings() };
        case 'owner_listing_details':
          return { outcome: await this.details(args) };
        case 'owner_update_listing':
          return this.update(args);
        case 'owner_confirm_availability':
          return this.availability(args);
        case 'owner_submit_listing':
          return this.submit(args);
        case 'owner_request_inspection':
          return this.inspection(args);
        case 'owner_visit_requests':
          return { outcome: await this.visits() };
        case 'owner_respond_to_visit':
          return this.respond(args);
        case 'owner_mark_visit_completed':
          return this.complete(args);
      }
      return { outcome: { result: { error: `There is no tool called ${name}.` } } };
    } catch (err) {
      return { outcome: toolFailure(err, this.router) };
    }
  }

  // ---------------------------------------------------------------- reading

  private async listings(): Promise<ToolOutcome> {
    const page = await firstValueFrom(this.owner.list(1, 50));
    return {
      result: {
        total: page.totalCount,
        listings: page.items.map((l) => ({
          propertyId: l.id,
          title: l.title,
          status: statusLabel(l.status),
          where: `${l.areaName}, ${l.cityName}`,
          rent: `${rupees(l.monthlyRent)}/month`,
          beds: `${l.availableBeds} of ${l.totalBeds} free`,
          photos: l.photoCount,
          inspectionBadge: l.hasInspectionBadge ? 'Yes' : 'No',
          availabilityLastConfirmed: day(l.availabilityConfirmedAt),
          lastUpdated: day(l.updatedAt),
          canSubmit: canSubmit(l.status),
          whySubmitWouldFail: canSubmit(l.status) ? submitProblem(l.status, l.photoCount) : undefined,
        })),
      },
      links: [{ label: 'Open my listings', commands: ['/owner'] }],
    };
  }

  private async details(args: Args): Promise<ToolOutcome> {
    const propertyId = asInt(args['propertyId']);
    if (!propertyId) return { result: { error: 'propertyId is required.' } };
    const l = await firstValueFrom(this.owner.detail(propertyId));
    const pending = l.photos.filter((p) => p.status !== 'Live').length;

    return {
      result: {
        propertyId: l.id,
        title: l.title,
        status: statusLabel(l.status),
        whatSavingDoes: effectSentence(l),
        description: l.description,
        address: l.addressLine,
        where: `${l.areaName}, ${l.cityName}`,
        roomType: l.roomType,
        forWhom: l.genderPolicy,
        beds: `${l.availableBeds} of ${l.totalBeds} free`,
        rent: rupees(l.monthlyRent),
        securityDeposit: rupees(l.securityDeposit),
        utilities: rupees(l.utilitiesCharge),
        mess: rupees(l.messCharge),
        houseRules: l.houseRules ?? 'none',
        facilities: l.facilities.map((f) => f.name),
        photos: `${livePhotoCount(l)} live${pending ? `, ${pending} waiting for review` : ''}`,
        inspectionBadge: l.hasInspectionBadge ? 'Yes' : 'No',
        availabilityLastConfirmed: day(l.availabilityConfirmedAt),
        createdOn: day(l.createdAt),
        lastUpdated: day(l.updatedAt),
        whySubmitWouldFail: canSubmit(l.status) ? submitProblem(l.status, livePhotoCount(l)) : undefined,
      },
      links: [
        { label: 'Edit on the listing form', commands: ['/owner/listing', l.id] },
        ...(l.status === 'Published' ? [{ label: 'See the public page', commands: ['/property', l.id] }] : []),
      ],
    };
  }

  private async visits(): Promise<ToolOutcome> {
    const page = await firstValueFrom(this.owner.visits(1, 50));
    return {
      result: { total: page.totalCount, visits: page.items.map((v) => describeVisit(v)) },
      links: [{ label: 'Open my listings', commands: ['/owner'] }],
    };
  }

  // ---------------------------------------------------------------- writing

  private async update(args: Args): Promise<Prepared> {
    const propertyId = asInt(args['propertyId']);
    if (!propertyId) return required('propertyId');
    const listing = await firstValueFrom(this.owner.detail(propertyId));

    if (saveEffect(listing.status) === 'locked') {
      return { outcome: { result: { error: 'The review team is reading this listing right now, so it cannot be changed until they finish.' } } };
    }

    const draft = toDraft(listing);
    const changes: string[] = [];
    const set = <K extends keyof ListingDraft>(key: K, value: ListingDraft[K] | undefined, label: string, show: (v: ListingDraft[K]) => string) => {
      if (value === undefined || value === draft[key]) return;
      changes.push(`${label}: ${show(draft[key])} → ${show(value)}`);
      draft[key] = value;
    };
    const text = (v: string) => `"${v}"`;
    const money = (v: number) => rupees(v);
    const plain = (v: unknown) => String(v);

    set('title', asText(args['title']), 'Title', text);
    set('description', asText(args['description']), 'Description', text);
    set('addressLine', asText(args['addressLine']), 'Address', text);
    set('houseRules', asText(args['houseRules']), 'House rules', text);
    set('monthlyRent', money0(args['monthlyRent']), 'Monthly rent', money);
    set('securityDeposit', money0(args['securityDeposit']), 'Security deposit', money);
    set('utilitiesCharge', money0(args['utilitiesCharge']), 'Utilities', money);
    set('messCharge', money0(args['messCharge']), 'Mess', money);
    set('totalBeds', asInt(args['totalBeds']), 'Total beds', plain);
    set('availableBeds', beds0(args['availableBeds']), 'Beds free', plain);
    set('roomType', toRoomType(args['roomType']), 'Room type', plain);
    set('genderPolicy', toGenderPolicy(args['genderPolicy']), 'For', plain);

    const catalogue = await firstValueFrom(this.reference.facilities());
    const add = matchFacilities(args['addFacilities'], catalogue);
    const remove = matchFacilities(args['removeFacilities'], catalogue);
    const nameOf = (id: number) => catalogue.find((f) => f.id === id)?.name ?? `#${id}`;
    const facilities = new Set(draft.facilityIds);
    // Asked-for facilities the listing already has (or already lacks) are said out loud, so
    // "add parking" on a listing with parking does not look like it was quietly ignored.
    const unchanged: string[] = [];
    for (const id of add.ids) {
      if (facilities.has(id)) unchanged.push(`${nameOf(id)} is already listed`);
      else {
        facilities.add(id);
        changes.push(`Add facility: ${nameOf(id)}`);
      }
    }
    for (const id of remove.ids) {
      if (facilities.delete(id)) changes.push(`Remove facility: ${nameOf(id)}`);
      else unchanged.push(`${nameOf(id)} is not listed`);
    }
    draft.facilityIds = [...facilities];

    const unknown = [...add.unknown, ...remove.unknown];
    if (draft.availableBeds > draft.totalBeds) {
      return { outcome: { result: { error: `Beds free (${draft.availableBeds}) cannot be more than total beds (${draft.totalBeds}).` } } };
    }
    if (!changes.length) {
      return {
        outcome: {
          result: {
            error:
              'Nothing would change.' +
              (unchanged.length ? ` ${unchanged.join('; ')}.` : '') +
              (unknown.length ? ` No such facility: ${unknown.join(', ')}.` : ''),
          },
        },
      };
    }

    const revision = saveEffect(listing.status) === 'revision';
    const summary =
      `${revision ? 'Send these changes to' : 'Save these changes to'} "${listing.title}" ` +
      `(${changes.join('; ')})` +
      (revision ? ' for admin review? Your live listing stays as it is until they approve.' : '?') +
      (unchanged.length ? ` (${unchanged.join('; ')}.)` : '') +
      (unknown.length ? ` (No such facility: ${unknown.join(', ')}.)` : '');

    return {
      pending: {
        summary,
        run: () =>
          this.writeListing(this.owner.update(propertyId, draft), (saved) =>
            saved.pendingRevision
              ? 'sent for review - the live listing has NOT changed yet; an admin approves it first. Never say "saved".'
              : 'saved',
          ),
      },
    };
  }

  private async availability(args: Args): Promise<Prepared> {
    const propertyId = asInt(args['propertyId']);
    const bedsFree = beds0(args['availableBeds']);
    if (!propertyId) return required('propertyId');
    if (bedsFree === undefined) return required('availableBeds');
    const listing = await firstValueFrom(this.owner.detail(propertyId));
    if (bedsFree > listing.totalBeds) {
      return { outcome: { result: { error: `"${listing.title}" has ${listing.totalBeds} beds in total.` } } };
    }
    return {
      pending: {
        summary: `Confirm ${bedsFree} of ${listing.totalBeds} beds free at "${listing.title}"?`,
        run: () =>
          this.writeListing(this.owner.setAvailability(propertyId, bedsFree), (saved) =>
            saved.pendingRevision ? 'availability sent for its quick review' : 'availability confirmed',
          ),
      },
    };
  }

  private async submit(args: Args): Promise<Prepared> {
    const propertyId = asInt(args['propertyId']);
    if (!propertyId) return required('propertyId');
    const listing = await firstValueFrom(this.owner.detail(propertyId));
    const problem = submitProblem(listing.status, livePhotoCount(listing));
    if (problem) {
      return {
        outcome: {
          result: { error: problem },
          links: [{ label: 'Open the listing form', commands: ['/owner/listing', propertyId] }],
        },
      };
    }
    return {
      pending: {
        summary: `Send "${listing.title}" to the review team?`,
        run: () => this.writeListing(this.owner.submit(propertyId), () => 'sent to the review team'),
      },
    };
  }

  private async inspection(args: Args): Promise<Prepared> {
    const propertyId = asInt(args['propertyId']);
    if (!propertyId) return required('propertyId');
    const listing = await firstValueFrom(this.owner.detail(propertyId));
    return {
      pending: {
        summary: `Ask RoomRaah to inspect "${listing.title}"? Each listing can ask once.`,
        run: async () => {
          try {
            const state = (await firstValueFrom(this.owner.requestInspection(propertyId))) as {
              inspectionRequestedAt?: string | null;
              hasInspectionBadge?: boolean;
              lastInspectionAt?: string | null;
            };
            return {
              result: {
                ok: true,
                done: 'inspection requested - somebody will be in touch to arrange it',
                requestedOn: day(state?.inspectionRequestedAt),
                hasBadge: state?.hasInspectionBadge ? 'Yes' : 'No',
                lastInspected: day(state?.lastInspectionAt),
              },
            };
          } catch (err) {
            return toolFailure(err, this.router);
          }
        },
      },
    };
  }

  private async respond(args: Args): Promise<Prepared> {
    const visitId = asInt(args['visitId']);
    if (!visitId) return required('visitId');
    const accept = /^accept/i.test(String(args['decision'] ?? ''));
    const decline = /^declin/i.test(String(args['decision'] ?? ''));
    if (!accept && !decline) return { outcome: { result: { error: 'decision must be Accepted or Declined.' } } };
    const note = asText(args['note']);
    if (decline && !note) {
      return { outcome: { result: { error: 'A decline needs a short note for the seeker. Ask the owner what to say.' } } };
    }

    const visit = await this.findVisit(visitId);
    if (!visit) return { outcome: { result: { error: 'No visit request on their listings has that id. Call owner_visit_requests.' } } };
    if (visit.status !== 'Requested') {
      return { outcome: { result: { error: `That request is already ${visit.status}; only a Requested one can be answered.` } } };
    }

    const who = `${visit.seekerDisplayName}'s ${visit.visitType === 'Video' ? 'video-call' : 'in-person'} visit to "${visit.propertyTitle}" on ${when(visit.preferredAt)}`;
    return {
      pending: {
        summary: accept ? `Accept ${who}?` : `Decline ${who}, telling them: "${note}"?`,
        run: () =>
          runWrite(
            this.owner.respondToVisit(visitId, accept ? 'Accepted' : 'Declined', note),
            { result: { ok: true, done: accept ? 'visit accepted' : 'visit declined, with the note sent to the seeker' } },
            this.router,
          ),
      },
    };
  }

  private async complete(args: Args): Promise<Prepared> {
    const visitId = asInt(args['visitId']);
    if (!visitId) return required('visitId');
    const visit = await this.findVisit(visitId);
    if (!visit) return { outcome: { result: { error: 'No visit request on their listings has that id.' } } };
    if (!canComplete(visit)) {
      return { outcome: { result: { error: 'Only an accepted visit whose time has passed can be marked completed.' } } };
    }
    return {
      pending: {
        summary: `Mark ${visit.seekerDisplayName}'s visit to "${visit.propertyTitle}" as completed?`,
        run: () => runWrite(this.owner.completeVisit(visitId), { result: { ok: true, done: 'visit marked completed' } }, this.router),
      },
    };
  }

  private async findVisit(visitId: number): Promise<VisitRequest | undefined> {
    const page = await firstValueFrom(this.owner.visits(1, 50));
    return page.items.find((v) => v.id === visitId);
  }

  /** A listing write, with the result worded from what the API actually answered. */
  private async writeListing(request: ReturnType<OwnerService['update']>, done: (saved: MyListing) => string): Promise<ToolOutcome> {
    try {
      const saved = await firstValueFrom(request);
      return {
        result: { ok: true, done: done(saved), status: statusLabel(saved.status) },
        links: [{ label: 'Open my listings', commands: ['/owner'] }],
      };
    } catch (err) {
      return toolFailure(err, this.router);
    }
  }
}

/** Rule 3, before the owner presses anything: what a save will actually do to this listing. */
export function effectSentence(listing: MyListing): string {
  switch (saveEffect(listing.status)) {
    case 'revision':
      return 'It is live, so any change goes to an admin for review first; the live listing stays as it is until approved.';
    case 'locked':
      return 'The review team is reading it now, so it cannot be changed until they finish.';
    default:
      return 'It is not live, so changes are saved straight away.';
  }
}

function describeVisit(v: VisitRequest) {
  return {
    visitId: v.id,
    hostel: v.propertyTitle,
    seeker: v.seekerDisplayName,
    type: v.visitType === 'Physical' ? 'In person' : 'Video call',
    when: when(v.preferredAt),
    status: v.status,
    yourNote: v.ownerResponseNote ?? 'none',
    requestedOn: day(v.createdAt),
    canRespond: v.status === 'Requested',
    canMarkCompleted: canComplete(v),
  };
}

/** Money and bed counts where zero is a real answer ("no mess charge", "no beds free"). */
function money0(value: unknown): number | undefined {
  if (value === 0 || value === '0') return 0;
  return asInt(value);
}

function beds0(value: unknown): number | undefined {
  return money0(value);
}
