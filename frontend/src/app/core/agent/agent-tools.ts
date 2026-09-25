import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, firstValueFrom, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { AuthService } from '../auth/auth.service';
import {
  LocationSuggestion,
  PAGE_SIZE_MAX,
  PropertyCard,
  PropertyFilters,
} from '../models/catalog.model';
import {
  REPORT_DETAILS_MAX,
  REPORT_REASONS,
  REVIEW_COMMENT_MAX,
  ReportReason,
  STAY_STATUSES,
  VisitRequest,
  VisitType,
  canCancel,
  canComplete,
  canReview,
  visitProblem,
} from '../models/engagement.model';
import { messageProblem } from '../models/messaging.model';
import { CompareStore } from '../services/compare.store';
import { EngagementService } from '../services/engagement.service';
import { GeolocationService } from '../services/geolocation.service';
import { MessagingService } from '../services/messaging.service';
import { COMPARE_LIMIT, PropertyService } from '../services/property.service';
import { ReferenceService } from '../services/reference.service';
import { HostelCard, ToolContext, ToolDefinition, ToolOutcome } from './agent.model';
import { Args, Prepared, runWrite, toolFailure, when } from './tool-support';
import { OWNER_TOOL_DEFINITIONS, OWNER_TOOL_NAMES, OwnerToolbox } from './owner-tools';
import { ADMIN_TOOL_DEFINITIONS, ADMIN_TOOL_NAMES, AdminToolbox } from './admin-tools';
import {
  asInt,
  asText,
  day,
  matchFacilities,
  rankByDistanceThenRent,
  rupees,
  toGenderPolicy,
  toRoomType,
} from './agent.util';

/** Contract of old, kept because it is right: the model is shown at most five at a time. */
export const TOP_N = 5;

/** How much the retry loosens the budget when nothing matched exactly. */
export const RELAXED_BUDGET = 1.2;

/** What "near" means when the person gives no distance, and how far a retry looks. */
export const NEAR_KM = 15;
export const WIDER_KM = 30;

export type { Prepared } from './tool-support';

const PAGES: Record<string, (id?: number) => unknown[] | null> = {
  home: () => ['/'],
  search: () => ['/search'],
  map_search: () => ['/search/map'],
  compare: () => ['/compare'],
  hostel: (id) => (id ? ['/property', id] : null),
  hostel_photos: (id) => (id ? ['/property', id, 'photos'] : null),
  hostel_reviews: (id) => (id ? ['/property', id, 'reviews'] : null),
  visit_request_form: (id) => (id ? ['/property', id, 'visit'] : null),
  write_review_form: (id) => (id ? ['/property', id, 'review'] : null),
  saved: () => ['/saved'],
  visits: () => ['/visits'],
  messages: () => ['/messages'],
  profile: () => ['/profile'],
  login: () => ['/login'],
  signup: () => ['/signup'],
  verify_email: () => ['/verify'],
  reset_password: () => ['/reset-password'],
  how_it_works: () => ['/how-it-works'],
  owner_dashboard: () => ['/owner'],
  owner_new_listing: () => ['/owner/listing', 'new'],
  owner_edit_listing: (id) => (id ? ['/owner/listing', id] : null),
  admin_listings: () => ['/admin/listings'],
  admin_revisions: () => ['/admin/revisions'],
  admin_reports_and_reviews: () => ['/admin/reports'],
  admin_inspections: () => ['/admin/inspections'],
  admin_users: () => ['/admin/users'],
};

/** The seeker's own lists and writes. Offered to a signed-out visitor too, so it can be told to sign in. */
const SEEKER_TOOLS = new Set([
  'my_saved_hostels',
  'my_visits',
  'save_hostel',
  'unsave_hostel',
  'request_visit',
  'cancel_visit',
  'mark_visit_completed',
  'send_message',
  'write_review',
  'report_listing',
]);

/** Messaging belongs to whoever is in the thread - the seeker who opened it or the owner answering. */
const ACCOUNT_TOOLS = new Set(['my_conversations', 'read_conversation', 'reply_in_conversation']);

function tool(name: string, description: string, properties: Args = {}, required: string[] = []): ToolDefinition {
  return { type: 'function', function: { name, description, parameters: { type: 'object', properties, required } } };
}

const id = (what: string) => ({ type: 'integer', description: `${what} id, from an earlier tool result` });

export const TOOL_DEFINITIONS: ToolDefinition[] = [
  tool(
    'search_hostels',
    'Search published rooms and hostels. Every filter is optional. Results come back ranked, with rent, beds, inspection status, rating and straight-line distance. Use this for any request to find, list or show hostels.',
    {
      place: { type: 'string', description: 'A city, area, university or workplace by name, e.g. "Lahore", "Gulberg", "Punjab University"' },
      nearMe: { type: 'boolean', description: "true when the person wants somewhere near their own current location" },
      maxRent: { type: 'integer', description: 'Monthly rent ceiling in rupees. "25k" or "25 hazar" means 25000' },
      minRent: { type: 'integer', description: 'Monthly rent floor in rupees' },
      genderPolicy: {
        type: 'string',
        enum: ['Boys', 'Girls', 'Family', 'Any'],
        description: 'Who it is for. "Any" when the person said it does not matter. Leave out when they have not said.',
      },
      roomType: { type: 'string', enum: ['Private', 'Shared'] },
      facilities: { type: 'array', items: { type: 'string' }, description: 'Facility names, e.g. ["WiFi", "Mess"]' },
      availableOnly: { type: 'boolean', description: 'Only listings with a free bed' },
      maxDistanceKm: { type: 'number', description: 'Only within this many km of the place or of the person' },
      sort: { type: 'string', enum: ['nearest', 'cheapest', 'most_expensive', 'newest'] },
    },
  ),
  tool('get_hostel_details', 'Everything the listing page shows about one hostel: description, address, facilities, house rules, costs, owner display name and identity check, verification checks with dates, inspection badge, availability, rating and photo count.', { propertyId: id('Hostel') }, ['propertyId']),
  tool('get_reviews', "A hostel's published reviews and average rating.", { propertyId: id('Hostel') }, ['propertyId']),
  tool('compare_hostels', 'Compare two or three hostels side by side, and fill the compare list with them.', { propertyIds: { type: 'array', items: { type: 'integer' }, description: '2 or 3 hostel ids' } }, ['propertyIds']),
  tool('add_to_compare', 'Tick a hostel for comparison (at most 3).', { propertyId: id('Hostel') }, ['propertyId']),
  tool('remove_from_compare', 'Untick a hostel from the compare list.', { propertyId: id('Hostel') }, ['propertyId']),
  tool('find_place', 'Look up cities, areas, universities and workplaces RoomRaah knows by name.', { query: { type: 'string' } }, ['query']),
  tool('list_facilities', 'The facilities a listing can have, by name.'),
  tool('list_cities', 'The cities RoomRaah covers.'),
  tool('use_my_location', "Ask the browser for the person's location so distances can be measured from them."),
  tool(
    'open_page',
    'Take the person to a page of the website, exactly as if they clicked a link.',
    {
      page: { type: 'string', enum: Object.keys(PAGES) },
      propertyId: { type: 'integer', description: 'Required for the hostel_* pages and the two forms' },
    },
    ['page'],
  ),
  tool('my_saved_hostels', "The person's shortlist of saved hostels."),
  tool('my_visits', "The person's visit requests with their status, and what can be done with each."),
  tool('my_conversations', "The person's message threads with owners."),
  tool('read_conversation', 'The latest messages in one thread.', { conversationId: id('Conversation') }, ['conversationId']),
  tool(
    'reply_in_conversation',
    'Send a message in an existing thread, as the signed-in person.',
    { conversationId: id('Conversation'), text: { type: 'string' } },
    ['conversationId', 'text'],
  ),
  tool('save_hostel', 'Save a hostel to the shortlist.', { propertyId: id('Hostel') }, ['propertyId']),
  tool('unsave_hostel', 'Remove a hostel from the shortlist.', { propertyId: id('Hostel') }, ['propertyId']),
  tool(
    'request_visit',
    'Ask the owner for a visit. The time must be in the future and at most 60 days ahead.',
    {
      propertyId: id('Hostel'),
      visitType: { type: 'string', enum: ['Physical', 'Video'] },
      preferredAt: { type: 'string', description: 'Local date and time as YYYY-MM-DDTHH:mm' },
    },
    ['propertyId', 'visitType', 'preferredAt'],
  ),
  tool('cancel_visit', 'Cancel a requested or accepted visit.', { visitId: id('Visit') }, ['visitId']),
  tool('mark_visit_completed', 'Mark an accepted visit as done, once its time has passed. This is what allows a review.', { visitId: id('Visit') }, ['visitId']),
  tool('send_message', "Send a message to a hostel's owner through RoomRaah messaging.", { propertyId: id('Hostel'), text: { type: 'string' } }, ['propertyId', 'text']),
  tool(
    'write_review',
    'Review a hostel. Only possible after a completed visit there. Reviews wait for moderation.',
    {
      propertyId: id('Hostel'),
      rating: { type: 'integer', description: '1 to 5' },
      comment: { type: 'string' },
      stayStatus: { type: 'string', enum: STAY_STATUSES.map((s) => s.value) },
    },
    ['propertyId', 'rating', 'comment', 'stayStatus'],
  ),
  tool(
    'report_listing',
    'Report a listing that is wrong, misleading, taken or a scam.',
    {
      propertyId: id('Hostel'),
      reason: { type: 'string', enum: REPORT_REASONS.map((r) => r.value) },
      details: { type: 'string' },
    },
    ['propertyId', 'reason', 'details'],
  ),
];

/**
 * Every tool the assistant has, and the one place each is carried out.
 *
 * Nothing here is new capability: every call goes through the same service a page uses, so
 * the assistant can do exactly what the person could do by clicking and nothing more - the
 * API still decides who may do what. Anything that writes comes back as a `pending` action
 * the person has to confirm, worded here from real data rather than from the model.
 */
@Injectable({ providedIn: 'root' })
export class AgentToolbox {
  private readonly properties = inject(PropertyService);
  private readonly reference = inject(ReferenceService);
  private readonly engagement = inject(EngagementService);
  private readonly messaging = inject(MessagingService);
  private readonly geo = inject(GeolocationService);
  private readonly compare = inject(CompareStore);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly ownerTools = inject(OwnerToolbox);
  private readonly adminTools = inject(AdminToolbox);

  /**
   * Each account is offered what its role can do and nothing else: the API would refuse the
   * rest anyway, and a tool the model cannot use is a tool it will still try.
   */
  definitions(): ToolDefinition[] {
    const role = this.auth.isAuthenticated() ? this.auth.userRole() : null;
    const common = TOOL_DEFINITIONS.filter((t) => !SEEKER_TOOLS.has(t.function.name) && !ACCOUNT_TOOLS.has(t.function.name));
    const messaging = TOOL_DEFINITIONS.filter((t) => ACCOUNT_TOOLS.has(t.function.name));
    if (role === 'Owner') return [...common, ...messaging, ...OWNER_TOOL_DEFINITIONS];
    if (role === 'Admin') return [...common, ...ADMIN_TOOL_DEFINITIONS];
    return TOOL_DEFINITIONS;
  }

  async prepare(name: string, args: Args, context: ToolContext = {}): Promise<Prepared> {
    try {
      switch (name) {
        case 'search_hostels':
          return { outcome: await this.search(args, context) };
        case 'get_hostel_details':
          return { outcome: await this.details(args) };
        case 'get_reviews':
          return { outcome: await this.reviews(args) };
        case 'compare_hostels':
          return { outcome: await this.compareHostels(args) };
        case 'add_to_compare':
          return { outcome: this.addToCompare(args) };
        case 'remove_from_compare':
          return { outcome: this.removeFromCompare(args) };
        case 'find_place':
          return { outcome: await this.findPlace(args) };
        case 'list_facilities':
          return { outcome: { result: (await firstValueFrom(this.reference.facilities())).map((f) => f.name) } };
        case 'list_cities':
          return { outcome: { result: (await firstValueFrom(this.reference.cities())).map((c) => c.name) } };
        case 'use_my_location':
          return { outcome: await this.useMyLocation() };
        case 'open_page':
          return { outcome: this.openPage(args) };
      }

      if (SEEKER_TOOLS.has(name) || ACCOUNT_TOOLS.has(name) || OWNER_TOOL_NAMES.has(name) || ADMIN_TOOL_NAMES.has(name)) {
        const needs = OWNER_TOOL_NAMES.has(name)
          ? 'Owner'
          : ADMIN_TOOL_NAMES.has(name)
            ? 'Admin'
            : SEEKER_TOOLS.has(name)
              ? 'Seeker'
              : null;
        const refusal = this.accountRefusal(needs);
        if (refusal) return { outcome: refusal };
      }
      if (OWNER_TOOL_NAMES.has(name)) return this.ownerTools.prepare(name, args);
      if (ADMIN_TOOL_NAMES.has(name)) return this.adminTools.prepare(name, args);

      switch (name) {
        case 'my_saved_hostels':
          return { outcome: await this.saved() };
        case 'my_visits':
          return { outcome: await this.visits() };
        case 'my_conversations':
          return { outcome: await this.conversations() };
        case 'read_conversation':
          return { outcome: await this.readConversation(args) };
        case 'reply_in_conversation':
          return this.reply(args);
        case 'save_hostel':
          return this.saveHostel(args, true);
        case 'unsave_hostel':
          return this.saveHostel(args, false);
        case 'request_visit':
          return this.requestVisit(args);
        case 'cancel_visit':
          return this.changeVisit(args, 'cancel');
        case 'mark_visit_completed':
          return this.changeVisit(args, 'complete');
        case 'send_message':
          return this.sendMessage(args);
        case 'write_review':
          return this.writeReview(args);
        case 'report_listing':
          return this.reportListing(args);
      }
      return { outcome: { result: { error: `There is no tool called ${name}.` } } };
    } catch (err) {
      return { outcome: this.failure(err) };
    }
  }

  // ---------------------------------------------------------------- searching

  private async search(args: Args, context: ToolContext): Promise<ToolOutcome> {
    const filters: PropertyFilters = { pageSize: PAGE_SIZE_MAX, sort: 'Recent' };
    const notes: string[] = [];

    // Who the room is for is the one thing asked before searching - RoomRaah does not know
    // a person's gender and must not guess it. The model often drops it from the call even
    // when the person said it, so their own words are the fallback; asked once and answered
    // "any", the search goes ahead unfiltered.
    const saidByModel = String(args['genderPolicy'] ?? '');
    const gender = toGenderPolicy(saidByModel) ?? (context.genderHint !== 'Any' ? context.genderHint : undefined);
    const anyone = /^any/i.test(saidByModel) || context.genderHint === 'Any';
    if (!gender && !anyone && !context.genderAsked) {
      return { result: { askFirst: 'gender', note: 'The website is asking the person who the room is for.' }, ask: 'gender' };
    }

    const place = asText(args['place']);
    let landmark: LocationSuggestion | null = null;
    if (place) {
      const resolved = await this.resolvePlace(place);
      if (!resolved) {
        return {
          result: {
            error: `RoomRaah does not know a place called "${place}". Tell the person, and ask for a nearby city or area. Do not suggest any place names yourself - only find_place results are real.`,
          },
        };
      }
      const hit = resolved.hit;
      if (!resolved.exact) {
        notes.push(`RoomRaah does not know "${place}" as a place, so the whole of ${hit.name} was searched instead. Tell the person that.`);
      }
      if (hit.type === 'City') filters.cityId = hit.id;
      if (hit.type === 'Area') filters.areaId = hit.id;
      if (hit.type === 'Landmark') {
        filters.landmarkId = hit.id;
        filters.sort = 'Distance';
        landmark = hit;
      }
      notes.push(`Place understood as ${hit.type.toLowerCase()} "${hit.name}"${hit.city ? `, ${hit.city}` : ''}.`);
    }

    const nearMe = args['nearMe'] === true;
    if (nearMe && !this.geo.hasPosition()) await this.askForPosition();
    if (nearMe && !this.geo.hasPosition()) {
      notes.push("The person's location is not available (the browser did not share it), so distance from them is unknown.");
    }

    filters.maxRent = asInt(args['maxRent']);
    filters.minRent = asInt(args['minRent']);
    if (filters.minRent && filters.maxRent && filters.minRent > filters.maxRent) filters.minRent = undefined;
    filters.genderPolicy = gender;
    filters.roomType = toRoomType(args['roomType']);
    if (args['availableOnly'] === true) filters.availableOnly = true;

    const facilities = matchFacilities(args['facilities'], await firstValueFrom(this.reference.facilities()));
    if (facilities.ids.length) filters.facilityIds = facilities.ids;
    if (facilities.unknown.length) {
      notes.push(`No such facility on RoomRaah, so not filtered on: ${facilities.unknown.join(', ')}.`);
    }

    // "Near NUST" means near it. Without a radius the nearest-first list still ends in other
    // cities, and a small model reads the top of it as "near" whatever the number says.
    const maxKm = Number(args['maxDistanceKm']);
    const askedKm = Number.isFinite(maxKm) && maxKm > 0 ? maxKm : undefined;
    const nearSomething = !!landmark || (nearMe && this.geo.hasPosition());
    const radius = askedKm ?? (nearSomething ? NEAR_KM : undefined);
    if (radius && landmark) filters.maxDistanceKm = radius;
    const origin = landmark ? landmark.name : 'the person';

    let found = await this.fetchRanked(filters, landmark, radius, args['sort']);
    let exactMatch = true;
    let searchedKm = radius;

    if (found.length === 0 && (filters.facilityIds || filters.maxRent || filters.roomType || filters.availableOnly || radius)) {
      searchedKm = radius ? Math.max(radius * 2, WIDER_KM) : undefined;
      const relaxed: PropertyFilters = {
        ...filters,
        facilityIds: undefined,
        roomType: undefined,
        availableOnly: undefined,
        maxDistanceKm: landmark ? searchedKm : undefined,
        maxRent: filters.maxRent ? Math.round(filters.maxRent * RELAXED_BUDGET) : undefined,
      };
      found = await this.fetchRanked(relaxed, landmark, searchedKm, args['sort']);
      exactMatch = false;
      notes.push(
        'Nothing matched every requirement, so the budget was loosened by 20%, the facility, room type and availability filters were dropped' +
          (searchedKm ? `, and the search widened to ${searchedKm} km of ${origin}` : '') +
          '. Say so plainly before listing these.',
      );
    }

    if (found.length === 0 && searchedKm) {
      // Nothing is near. Saying how far the nearest one is answers the real question.
      const anywhere: PropertyFilters = { pageSize: PAGE_SIZE_MAX, sort: filters.sort, landmarkId: filters.landmarkId, genderPolicy: filters.genderPolicy };
      const nearest = (await this.fetchRanked(anywhere, landmark, undefined, undefined))[0];
      const km = nearest ? (landmark ? nearest.distanceKm : this.geo.distanceTo(nearest.latitude, nearest.longitude)) : null;
      notes.push(
        nearest && km != null
          ? `Nothing is listed within ${searchedKm} km of ${origin}. The nearest ${filters.genderPolicy ?? ''} listing on RoomRaah is "${nearest.title}" in ${nearest.areaName}, ${nearest.cityName}, ${km.toFixed(1)} km away (straight-line). Tell the person nothing is near, and mention this only as the nearest there is.`
          : `Nothing is listed within ${searchedKm} km of ${origin}, and nothing else matches either.`,
      );
    }

    const top = found.slice(0, TOP_N);
    const ratings = await this.ratingsFor(top.map((c) => c.id));
    const cards = top.map((card) => this.toHostelCard(card, landmark, ratings.get(card.id) ?? null));

    return {
      result: {
        exactMatch,
        totalMatching: found.length,
        shown: top.length,
        notes,
        hostels: top.map((card, i) => this.summarise(card, cards[i], landmark, ratings.get(card.id) ?? null)),
      },
      cards,
      links: top.length
        ? [{ label: 'See all on the search page', commands: ['/search'], queryParams: this.searchQuery(filters) }]
        : undefined,
    };
  }

  private async fetchRanked(
    filters: PropertyFilters,
    landmark: LocationSuggestion | null,
    withinKm: number | undefined,
    sortArg: unknown,
  ): Promise<PropertyCard[]> {
    const page = await firstValueFrom(this.properties.search(filters));
    let items = page.items ?? [];

    const fromMe = (c: PropertyCard) => this.geo.distanceTo(c.latitude, c.longitude);
    const primary = (c: PropertyCard) => (landmark ? c.distanceKm : fromMe(c));

    if (withinKm && !landmark && this.geo.hasPosition()) {
      items = items.filter((c) => (fromMe(c) ?? Infinity) <= withinKm);
    }

    switch (sortArg) {
      case 'cheapest':
        return [...items].sort((a, b) => a.monthlyRent - b.monthlyRent);
      case 'most_expensive':
        return [...items].sort((a, b) => b.monthlyRent - a.monthlyRent);
      case 'newest':
        return items;
      default:
        return rankByDistanceThenRent(items, primary);
    }
  }

  /**
   * The name as typed, then - when RoomRaah does not know it - the city it mentions.
   * "FAST University Islamabad" is not a landmark here, and "islamabaad" is how half the
   * country spells the city; both should still search Islamabad rather than nothing, with a
   * note so the reply can say the university itself is not known.
   */
  private async resolvePlace(name: string): Promise<{ hit: LocationSuggestion; exact: boolean } | null> {
    const list = await firstValueFrom(this.reference.suggest(name));
    if (list.length) {
      const exact = list.find((s) => s.name.toLowerCase() === name.toLowerCase());
      return { hit: exact ?? list[0], exact: true };
    }

    // "NUST Islamabad" is not a name the API knows, but NUST is: a landmark or an area named
    // by one word of it beats falling back to the whole city.
    const cities = await firstValueFrom(this.reference.cities());
    const cityWords = new Set(cities.map((c) => squashLetters(c.name)));
    const tokens = name
      .split(/[^A-Za-z0-9-]+/)
      .filter((t) => t.length >= 3 && !cityWords.has(squashLetters(t)) && !GENERIC_PLACE_WORDS.test(t));
    for (const token of tokens) {
      const found = await firstValueFrom(this.reference.suggest(token));
      // A whole word of the name, so "Punjab" finds "University of the Punjab" but "Pun" does not.
      const hit = found.find(
        (s) => s.type !== 'City' && squashLetters(s.name).split(/[^a-z0-9]+/).includes(squashLetters(token)),
      );
      if (hit) return { hit, exact: true };
    }

    const words = squashLetters(name).split(/[^a-z]+/);
    const city = cities.find((c) => words.includes(squashLetters(c.name)));
    if (city) return { hit: { type: 'City', id: city.id, name: city.name }, exact: false };
    return null;
  }

  private async ratingsFor(ids: number[]): Promise<Map<number, { avg: number; count: number } | null>> {
    const pairs = await Promise.all(
      ids.map((propertyId) =>
        firstValueFrom(
          this.properties.reviews(propertyId, 1, 1).pipe(
            map((page) => (page.totalCount > 0 ? { avg: page.averageRating, count: page.totalCount } : null)),
            catchError(() => of(null)),
          ),
        ).then((rating) => [propertyId, rating] as const),
      ),
    );
    return new Map(pairs);
  }

  private distanceLabel(card: PropertyCard, landmark: LocationSuggestion | null): string | null {
    if (landmark && card.distanceKm != null) {
      return `${card.distanceKm.toFixed(1)} km from ${landmark.name} (straight-line)`;
    }
    const km = this.geo.distanceTo(card.latitude, card.longitude);
    return km === null ? null : `${km.toFixed(1)} km from you (straight-line)`;
  }

  private toHostelCard(
    card: PropertyCard,
    landmark: LocationSuggestion | null,
    rating: { avg: number; count: number } | null,
  ): HostelCard {
    return {
      id: card.id,
      title: card.title,
      location: `${card.areaName}, ${card.cityName}`,
      monthlyRent: card.monthlyRent,
      genderPolicy: card.genderPolicy,
      roomType: card.roomType,
      availableBeds: card.availableBeds,
      totalBeds: card.totalBeds,
      thumbnailUrl: card.primaryThumbnailUrl,
      inspected: card.hasInspectionBadge,
      distanceLabel: this.distanceLabel(card, landmark),
      averageRating: rating ? rating.avg : null,
    };
  }

  /** What the model reads about a listing: already worded, so it has nothing to work out. */
  private summarise(
    card: PropertyCard,
    view: HostelCard,
    landmark: LocationSuggestion | null,
    rating: { avg: number; count: number } | null,
  ) {
    const fromMe = this.geo.distanceTo(card.latitude, card.longitude);
    return {
      id: card.id,
      title: card.title,
      location: view.location,
      rent: `${rupees(card.monthlyRent)}/month`,
      monthlyTotal: `${rupees(card.monthlyRent + card.utilitiesCharge + card.messCharge)} with utilities and mess`,
      securityDeposit: rupees(card.securityDeposit),
      forWhom: card.genderPolicy,
      roomType: card.roomType,
      beds: `${card.availableBeds} of ${card.totalBeds} free`,
      distance: view.distanceLabel ?? 'not available',
      distanceFromYou:
        landmark && fromMe !== null ? `${fromMe.toFixed(1)} km from you (straight-line)` : undefined,
      inspected: card.hasInspectionBadge ? 'Yes - has the RoomRaah inspection badge' : 'Not yet',
      availabilityLastConfirmed: day(card.availabilityConfirmedAt),
      rating: rating ? `${rating.avg.toFixed(1)}/5 from ${rating.count} review(s)` : 'no reviews yet',
      photos: card.photoCount,
    };
  }

  private searchQuery(filters: PropertyFilters): Record<string, unknown> {
    const query: Record<string, unknown> = {};
    for (const [key, value] of Object.entries(filters)) {
      if (key === 'pageSize' || value === undefined || value === null) continue;
      query[key] = Array.isArray(value) ? value.join(',') : value;
    }
    return query;
  }

  // ---------------------------------------------------------------- one listing

  private async details(args: Args): Promise<ToolOutcome> {
    const propertyId = asInt(args['propertyId']);
    if (!propertyId) return { result: { error: 'propertyId is required.' } };

    const [detail, ratings] = await Promise.all([
      firstValueFrom(this.properties.detail(propertyId)),
      this.ratingsFor([propertyId]),
    ]);
    const rating = ratings.get(propertyId) ?? null;
    const card = this.toHostelCard(detail, null, rating);

    return {
      result: {
        ...this.summarise(detail, card, null, rating),
        description: detail.description,
        address: detail.addressLine,
        houseRules: detail.houseRules ?? 'none listed',
        facilities: detail.facilities.map((f) => f.name),
        utilities: rupees(detail.utilitiesCharge),
        mess: rupees(detail.messCharge),
        owner: {
          displayName: detail.owner.displayName,
          identityVerified: detail.owner.identityVerified ? 'Yes' : 'No',
          identityCheckedOn: day(detail.owner.identityCheckedAt),
          howToReach: 'Only through RoomRaah messages or a visit request.',
        },
        verificationChecks: detail.checks.map((c) => ({
          check: c.checkType,
          status: c.status,
          date: day(c.evidenceDate),
        })),
        listedOn: day(detail.createdAt),
      },
      cards: [card],
      links: [{ label: 'Open this listing', commands: ['/property', propertyId] }],
    };
  }

  private async reviews(args: Args): Promise<ToolOutcome> {
    const propertyId = asInt(args['propertyId']);
    if (!propertyId) return { result: { error: 'propertyId is required.' } };
    const page = await firstValueFrom(this.properties.reviews(propertyId, 1, 10));
    return {
      result: {
        averageRating: page.totalCount ? `${page.averageRating.toFixed(1)}/5` : 'no reviews yet',
        totalReviews: page.totalCount,
        reviews: page.items.map((r) => ({
          by: r.authorDisplayName,
          rating: r.rating,
          stay: r.stayStatus,
          comment: r.comment,
          on: day(r.createdAt),
        })),
      },
      links: [{ label: 'All reviews', commands: ['/property', propertyId, 'reviews'] }],
    };
  }

  private async compareHostels(args: Args): Promise<ToolOutcome> {
    const ids = (Array.isArray(args['propertyIds']) ? args['propertyIds'] : [])
      .map(asInt)
      .filter((n): n is number => !!n)
      .slice(0, COMPARE_LIMIT);
    if (ids.length < 2) return { result: { error: 'Give two or three hostel ids to compare.' } };

    const list = await firstValueFrom(this.properties.compare(ids));
    const ratings = await this.ratingsFor(list.map((c) => c.id));
    const cards = list.map((c) => this.toHostelCard(c, null, ratings.get(c.id) ?? null));

    this.compare.clear();
    for (const c of list) this.compare.toggle(c.id);

    return {
      result: {
        compared: list.map((c, i) => this.summarise(c, cards[i], null, ratings.get(c.id) ?? null)),
        note: list.length < ids.length ? 'Some of those ids are not published any more and were left out.' : undefined,
      },
      cards,
      links: [{ label: 'Open the comparison', commands: ['/compare'], queryParams: { ids: this.compare.query() } }],
    };
  }

  private addToCompare(args: Args): ToolOutcome {
    const propertyId = asInt(args['propertyId']);
    if (!propertyId) return { result: { error: 'propertyId is required.' } };
    if (!this.compare.has(propertyId)) {
      if (this.compare.full()) {
        return { result: { error: `The compare list already has ${COMPARE_LIMIT} hostels. Remove one first.`, compareList: this.compare.ids() } };
      }
      this.compare.toggle(propertyId);
    }
    return this.compareState();
  }

  private removeFromCompare(args: Args): ToolOutcome {
    const propertyId = asInt(args['propertyId']);
    if (propertyId && this.compare.has(propertyId)) this.compare.toggle(propertyId);
    return this.compareState();
  }

  private compareState(): ToolOutcome {
    const ids = this.compare.ids();
    return {
      result: { compareList: ids, count: `${ids.length} of ${COMPARE_LIMIT}` },
      links: ids.length ? [{ label: `Compare ${ids.length} chosen`, commands: ['/compare'], queryParams: { ids: this.compare.query() } }] : undefined,
    };
  }

  private async findPlace(args: Args): Promise<ToolOutcome> {
    const query = asText(args['query']);
    if (!query) return { result: { error: 'query is required.' } };
    const list = await firstValueFrom(this.reference.suggest(query));
    return {
      result: list.length
        ? list.slice(0, 8).map((s) => ({ type: s.type, name: s.name, city: s.city, kind: s.kind }))
        : { error: `Nothing called "${query}".` },
    };
  }

  private async useMyLocation(): Promise<ToolOutcome> {
    if (this.geo.state() === 'unavailable') {
      return { result: { error: 'This browser cannot share a location here (it needs https or localhost).' } };
    }
    await this.askForPosition();
    return {
      result: this.geo.hasPosition()
        ? { ok: true, note: 'Distances are now measured from the person.' }
        : { error: 'The person did not share their location.' },
    };
  }

  /** Waits for the browser's answer, since the permission prompt is the person's to click. */
  private async askForPosition(): Promise<void> {
    if (this.geo.hasPosition() || this.geo.state() === 'unavailable') return;
    this.geo.request();
    const deadline = Date.now() + 15_000;
    while (this.geo.state() === 'asking' && Date.now() < deadline) {
      await new Promise((resolve) => setTimeout(resolve, 250));
    }
  }

  private openPage(args: Args): ToolOutcome {
    const page = asText(args['page']) ?? '';
    const build = PAGES[page];
    if (!build) return { result: { error: `Unknown page "${page}".` } };
    const commands = build(asInt(args['propertyId']));
    if (!commands) return { result: { error: `The ${page} page needs a propertyId.` } };

    const queryParams = page === 'compare' && this.compare.count() ? { ids: this.compare.query() } : undefined;
    this.router.navigate(commands, { queryParams });
    return { result: { opened: page } };
  }

  // ---------------------------------------------------------------- the seeker's own

  /**
   * The same three answers every save button gives: sign in, verify (rule 3 - a 403 is
   * `/verify`, never a toast), or not a seeker's account.
   */
  private accountRefusal(role: 'Seeker' | 'Owner' | 'Admin' | null): ToolOutcome | null {
    if (!this.auth.isAuthenticated()) {
      return {
        result: { error: 'The person is not signed in. Ask them to sign in first; a Sign in button is shown under your reply.' },
        links: [{ label: 'Sign in', commands: ['/login'], queryParams: { returnUrl: this.router.url } }],
      };
    }
    if (!this.auth.isEmailVerified()) {
      this.router.navigate(['/verify']);
      return {
        result: { error: 'Their email is not verified yet, so they cannot act. They have been taken to the verify page.' },
        links: [{ label: 'Verify your email', commands: ['/verify'] }],
      };
    }
    if (role && this.auth.userRole() !== role) {
      return { result: { error: `Only ${role === 'Seeker' ? 'a seeker' : role === 'Owner' ? 'an owner' : 'an admin'} account can do this.` } };
    }
    return null;
  }

  private async saved(): Promise<ToolOutcome> {
    const page = await firstValueFrom(this.engagement.saved(1, 50));
    const cards = page.items.map((c) => this.toHostelCard(c, null, null));
    return {
      result: {
        total: page.totalCount,
        hostels: page.items.map((c, i) => ({ ...this.summarise(c, cards[i], null, null), rating: undefined, savedOn: day(c.savedAt) })),
      },
      cards: cards.slice(0, TOP_N),
      links: [{ label: 'Open saved', commands: ['/saved'] }],
    };
  }

  private async visits(): Promise<ToolOutcome> {
    const page = await firstValueFrom(this.engagement.myVisits(1, 50));
    return {
      result: {
        total: page.totalCount,
        visits: page.items.map((v) => this.describeVisit(v)),
      },
      links: [{ label: 'Open my visits', commands: ['/visits'] }],
    };
  }

  private describeVisit(v: VisitRequest) {
    return {
      visitId: v.id,
      hostel: v.propertyTitle,
      propertyId: v.propertyId,
      where: `${v.areaName}, ${v.cityName}`,
      type: v.visitType === 'Physical' ? 'In person' : 'Video call',
      when: when(v.preferredAt),
      status: v.status,
      ownerReply: v.ownerResponseNote ?? 'none',
      canCancel: canCancel(v),
      canMarkCompleted: canComplete(v),
      canReview: canReview(v),
    };
  }

  private async conversations(): Promise<ToolOutcome> {
    const page = await firstValueFrom(this.messaging.conversations(1, 50));
    return {
      result: page.items.map((c) => ({
        conversationId: c.id,
        hostel: c.propertyTitle,
        propertyId: c.propertyId,
        with: c.otherParticipantDisplayName,
        lastMessage: when(c.lastMessageAt),
        unread: c.unreadCount,
      })),
      links: [{ label: 'Open messages', commands: ['/messages'] }],
    };
  }

  private async readConversation(args: Args): Promise<ToolOutcome> {
    const conversationId = asInt(args['conversationId']);
    if (!conversationId) return { result: { error: 'conversationId is required.' } };
    const page = await firstValueFrom(this.messaging.messages(conversationId, 1, 50));
    return {
      result: page.items.slice(-15).map((m) => ({
        from: m.isMine ? 'the person' : m.senderDisplayName,
        text: m.body,
        sent: when(m.sentAt),
        read: m.readAt ? 'yes' : 'no',
      })),
      links: [{ label: 'Open this thread', commands: ['/messages'], queryParams: { c: conversationId } }],
    };
  }

  private async titleOf(propertyId: number): Promise<string> {
    return firstValueFrom(
      this.properties.detail(propertyId).pipe(
        map((d) => `"${d.title}"`),
        catchError(() => of(`listing #${propertyId}`)),
      ),
    );
  }

  private async saveHostel(args: Args, save: boolean): Promise<Prepared> {
    const propertyId = asInt(args['propertyId']);
    if (!propertyId) return { outcome: { result: { error: 'propertyId is required.' } } };

    // Asked first, because the API answers a second save with a 422 and there is no point
    // making somebody confirm an action that can only be refused.
    const shortlist = await firstValueFrom(this.engagement.saved(1, 50));
    const already = shortlist.items.some((item) => item.id === propertyId);
    if (save === already) {
      return {
        outcome: {
          result: { ok: true, done: false, note: already ? 'It is already on their shortlist.' : 'It is not on their shortlist.' },
          links: [{ label: 'Open saved', commands: ['/saved'] }],
        },
      };
    }

    const title = await this.titleOf(propertyId);
    return {
      pending: {
        summary: save ? `Save ${title} to your shortlist?` : `Remove ${title} from your shortlist?`,
        run: () =>
          this.write(save ? this.engagement.save(propertyId) : this.engagement.unsave(propertyId), {
            result: { ok: true, done: save ? 'saved to shortlist' : 'removed from shortlist' },
            links: [{ label: 'Open saved', commands: ['/saved'] }],
          }),
      },
    };
  }

  private async requestVisit(args: Args): Promise<Prepared> {
    const propertyId = asInt(args['propertyId']);
    const preferred = asText(args['preferredAt']) ?? '';
    const visitType: VisitType = /video/i.test(String(args['visitType'] ?? '')) ? 'Video' : 'Physical';
    if (!propertyId) return { outcome: { result: { error: 'propertyId is required.' } } };

    const problem = visitProblem(preferred);
    if (problem) return { outcome: { result: { error: problem } } };
    if (this.engagement.hasOpenVisit(propertyId)) {
      return { outcome: { result: { error: 'The person already has an open visit request for this hostel.' } } };
    }

    const title = await this.titleOf(propertyId);
    const how = visitType === 'Video' ? 'a video-call visit' : 'an in-person visit';
    return {
      pending: {
        summary: `Request ${how} to ${title} on ${when(preferred)}?`,
        run: () =>
          this.write(
            this.engagement.requestVisit({ propertyId, visitType, preferredAt: new Date(preferred).toISOString() }),
            {
              result: { ok: true, done: 'visit requested - the owner will accept or decline it' },
              links: [{ label: 'Open my visits', commands: ['/visits'] }],
            },
          ),
      },
    };
  }

  private async changeVisit(args: Args, action: 'cancel' | 'complete'): Promise<Prepared> {
    const visitId = asInt(args['visitId']);
    if (!visitId) return { outcome: { result: { error: 'visitId is required.' } } };
    const page = await firstValueFrom(this.engagement.myVisits(1, 50));
    const visit = page.items.find((v) => v.id === visitId);
    if (!visit) return { outcome: { result: { error: 'No visit of theirs has that id. Call my_visits.' } } };

    if (action === 'cancel' && !canCancel(visit)) {
      return { outcome: { result: { error: `A ${visit.status} visit cannot be cancelled.` } } };
    }
    if (action === 'complete' && !canComplete(visit)) {
      return { outcome: { result: { error: 'Only an accepted visit whose time has passed can be marked completed.' } } };
    }

    const summary =
      action === 'cancel'
        ? `Cancel your visit to "${visit.propertyTitle}" on ${when(visit.preferredAt)}?`
        : `Mark your visit to "${visit.propertyTitle}" as completed?`;
    return {
      pending: {
        summary,
        run: () =>
          this.write(action === 'cancel' ? this.engagement.cancelVisit(visitId) : this.engagement.completeVisit(visitId), {
            result: { ok: true, done: action === 'cancel' ? 'visit cancelled' : 'visit marked completed - a review can be written now' },
            links: [{ label: 'Open my visits', commands: ['/visits'] }],
          }),
      },
    };
  }

  private async sendMessage(args: Args): Promise<Prepared> {
    const propertyId = asInt(args['propertyId']);
    const text = asText(args['text']) ?? '';
    if (!propertyId) return { outcome: { result: { error: 'propertyId is required.' } } };
    const problem = messageProblem(text);
    if (problem) return { outcome: { result: { error: problem } } };

    const title = await this.titleOf(propertyId);
    return {
      pending: {
        summary: `Send this to the owner of ${title}: "${text}"`,
        run: async () => {
          const conversation = await firstValueFrom(this.messaging.open(propertyId));
          return this.write(this.messaging.send(conversation.id, text), {
            result: { ok: true, done: 'message sent' },
            links: [{ label: 'Open this thread', commands: ['/messages'], queryParams: { c: conversation.id } }],
          });
        },
      },
    };
  }

  private async reply(args: Args): Promise<Prepared> {
    const conversationId = asInt(args['conversationId']);
    const text = asText(args['text']) ?? '';
    if (!conversationId) return { outcome: { result: { error: 'conversationId is required.' } } };
    const problem = messageProblem(text);
    if (problem) return { outcome: { result: { error: problem } } };

    const threads = await firstValueFrom(this.messaging.conversations(1, 50));
    const thread = threads.items.find((c) => c.id === conversationId);
    if (!thread) return { outcome: { result: { error: 'They have no thread with that id. Call my_conversations.' } } };

    return {
      pending: {
        summary: `Send to ${thread.otherParticipantDisplayName} about "${thread.propertyTitle}": "${text}"`,
        run: () =>
          this.write(this.messaging.send(conversationId, text), {
            result: { ok: true, done: 'message sent' },
            links: [{ label: 'Open this thread', commands: ['/messages'], queryParams: { c: conversationId } }],
          }),
      },
    };
  }

  private async writeReview(args: Args): Promise<Prepared> {
    const propertyId = asInt(args['propertyId']);
    const rating = asInt(args['rating']);
    const comment = asText(args['comment']) ?? '';
    const stay = STAY_STATUSES.find((s) => s.value.toLowerCase() === String(args['stayStatus'] ?? '').toLowerCase());
    if (!propertyId) return { outcome: { result: { error: 'propertyId is required.' } } };
    if (!rating || rating > 5) return { outcome: { result: { error: 'rating must be a whole number from 1 to 5.' } } };
    if (!comment || comment.length > REVIEW_COMMENT_MAX) {
      return { outcome: { result: { error: `The comment must be 1 to ${REVIEW_COMMENT_MAX} characters.` } } };
    }
    if (!stay) return { outcome: { result: { error: 'Ask whether they visited, are staying now, or stayed before.' } } };

    const title = await this.titleOf(propertyId);
    return {
      pending: {
        summary: `Post a ${rating}-star review of ${title}: "${comment}"`,
        run: () =>
          this.write(this.engagement.writeReview({ propertyId, rating, comment, stayStatus: stay.value }), {
            result: { ok: true, done: 'review submitted - it appears once a moderator approves it' },
          }),
      },
    };
  }

  private async reportListing(args: Args): Promise<Prepared> {
    const propertyId = asInt(args['propertyId']);
    const details = asText(args['details']) ?? '';
    const reason = REPORT_REASONS.find((r) => r.value.toLowerCase() === String(args['reason'] ?? '').toLowerCase());
    if (!propertyId) return { outcome: { result: { error: 'propertyId is required.' } } };
    if (!reason) return { outcome: { result: { error: 'Pick a reason: ' + REPORT_REASONS.map((r) => r.value).join(', ') } } };
    if (!details || details.length > REPORT_DETAILS_MAX) {
      return { outcome: { result: { error: `Details must be 1 to ${REPORT_DETAILS_MAX} characters.` } } };
    }

    const title = await this.titleOf(propertyId);
    return {
      pending: {
        summary: `Report ${title} as "${reason.label}": "${details}"`,
        run: () =>
          this.write(this.engagement.fileReport({ propertyId, reason: reason.value as ReportReason, details }), {
            result: { ok: true, done: 'report filed - an admin will review it' },
          }),
      },
    };
  }

  private write(request: Observable<unknown>, success: ToolOutcome): Promise<ToolOutcome> {
    return runWrite(request, success, this.router);
  }

  private failure(err: unknown): ToolOutcome {
    return toolFailure(err, this.router);
  }
}

/**
 * Words that describe a kind of place rather than name one. "University" alone would match
 * University of Karachi, so "FAST University" must not be allowed to become it.
 */
const GENERIC_PLACE_WORDS =
  /^(univ\w*|uni|college|campus|institute|school|the|of|and|near|paas|pas|ke|ki|hostel|hostels|room|rooms|area|city|road|sector|block|phase|town)$/i;

/** Lower case, letters only, runs of one letter collapsed: "Islamabaad" and "islamabad" meet. */
function squashLetters(text: string): string {
  return text.toLowerCase().replace(/(.)\1+/g, '$1');
}
