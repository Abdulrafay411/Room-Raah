import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { CHECK_TYPES, ReportStatus, ReviewState, RevisionKind, inspectionProblem } from '../models/admin.model';
import { VerificationCheckType, VerificationStatus } from '../models/catalog.model';
import { PropertyStatus, statusLabel } from '../models/owner.model';
import { AdminService } from '../services/admin.service';
import { ToolDefinition, ToolOutcome } from './agent.model';
import { asInt, asText, day, rupees } from './agent.util';
import { Args, Prepared, required, runWrite, toolFailure, when } from './tool-support';

function tool(name: string, description: string, properties: Args = {}, required: string[] = []): ToolDefinition {
  return { type: 'function', function: { name, description, parameters: { type: 'object', properties, required } } };
}

const note = (what: string) => ({ type: 'string', description: what });
const propertyId = { type: 'integer', description: 'Listing id, from admin_listings' };

const STATUSES: PropertyStatus[] = ['Draft', 'PendingReview', 'ChangesRequested', 'Published', 'Rejected', 'Unpublished'];

export const ADMIN_TOOL_DEFINITIONS: ToolDefinition[] = [
  tool(
    'admin_listings',
    'LISTINGS (hostels/rooms) by status, with owner name. For "pending listings", "listings to review", "approval queue" or "kaunsi listings pending hain", pass status PendingReview.',
    { status: { type: 'string', enum: STATUSES } },
  ),
  tool('admin_listing_details', 'One listing in full: owner and identity status, the verification checks with dates and notes, whether it can be published, and the badge.', { propertyId }, ['propertyId']),
  tool(
    'admin_record_verification',
    'Record one verification check on a listing. OwnerIdentity, Address, Photos and Facilities must all be Checked before it can be published.',
    {
      propertyId,
      checkType: { type: 'string', enum: CHECK_TYPES },
      status: { type: 'string', enum: ['Checked', 'Failed', 'NotChecked'] },
      evidenceDate: { type: 'string', description: 'YYYY-MM-DD, when the evidence was seen' },
      note: note('What was seen'),
    },
    ['propertyId', 'checkType', 'status'],
  ),
  tool(
    'admin_decide_listing',
    'Approve, reject, ask for changes to, or unpublish a listing. Every decision except approve needs a note, which the owner sees.',
    {
      propertyId,
      action: { type: 'string', enum: ['approve', 'reject', 'request-changes', 'unpublish'] },
      note: note('Required except on approve'),
    },
    ['propertyId', 'action'],
  ),
  tool('admin_revisions', 'Pending revisions. Content is a before-and-after review; Availability is a bed count to confirm.', {
    kind: { type: 'string', enum: ['Content', 'Availability'] },
  }, ['kind']),
  tool('admin_revision_details', 'One revision: every field current → proposed, and the photographs it adds or removes.', {
    revisionId: { type: 'integer' },
  }, ['revisionId']),
  tool(
    'admin_decide_revision',
    'Approve or reject a revision. A rejection needs a note for the owner.',
    { revisionId: { type: 'integer' }, action: { type: 'string', enum: ['approve', 'reject'] }, note: note('Required on reject') },
    ['revisionId', 'action'],
  ),
  tool('admin_due_inspections', 'Listings due for an inspection, most overdue first.'),
  tool(
    'admin_record_inspection',
    'Record an inspection that has happened. Only an admin with inspection permission can.',
    {
      propertyId,
      inspectedAt: { type: 'string', description: 'Local date and time it happened, YYYY-MM-DDTHH:mm; not in the future' },
      result: { type: 'string', enum: ['Passed', 'Failed'] },
      notes: note('Optional'),
      feeAmount: { type: 'integer', description: 'Rupees, if a fee was collected' },
    },
    ['propertyId', 'inspectedAt', 'result'],
  ),
  tool(
    'admin_change_badge',
    'Grant or remove the inspection badge. Granting needs inspection permission; any admin may remove. A reason is required.',
    { propertyId, action: { type: 'string', enum: ['grant', 'remove'] }, reason: note('Required') },
    ['propertyId', 'action', 'reason'],
  ),
  tool(
    'admin_reviews',
    'Star-rating REVIEWS that seekers wrote about hostels - not listings. New ones are Pending and invisible until approved. Leave state out for the Pending ones.',
    { state: { type: 'string', enum: ['Pending', 'Approved', 'Hidden', 'All'] } },
  ),
  tool('admin_moderate_review', 'Approve a review (it becomes public) or hide it.', {
    reviewId: { type: 'integer' },
    state: { type: 'string', enum: ['Approved', 'Hidden'] },
  }, ['reviewId', 'state']),
  tool(
    'admin_reports',
    'Reports seekers filed about listings. Leave status out for the unresolved ones (Open and UnderReview); Upheld and Dismissed are already decided.',
    { status: { type: 'string', enum: ['Open', 'UnderReview', 'Upheld', 'Dismissed', 'All'] } },
  ),
  tool(
    'admin_resolve_report',
    'Move a report to UnderReview, or give a verdict (Upheld or Dismissed) with a note. Upholding does NOT unpublish the listing.',
    {
      reportId: { type: 'integer' },
      status: { type: 'string', enum: ['UnderReview', 'Upheld', 'Dismissed'] },
      note: note('Required for Upheld and Dismissed'),
    },
    ['reportId', 'status'],
  ),
  tool('admin_users', 'Accounts, searchable by name or email. Leave isActive out to include deactivated ones.', {
    role: { type: 'string', enum: ['Seeker', 'Owner', 'Admin'] },
    isActive: { type: 'boolean' },
    query: { type: 'string', description: 'Part of a name or email, 2+ characters' },
  }),
  tool('admin_user_details', 'One account: role, verification, active, identity check, inspection permission.', { userId: { type: 'integer' } }, ['userId']),
  tool(
    'admin_set_user_active',
    'Deactivate or reactivate an account, with a reason. Not your own account and not another admin. Deactivating an owner takes their listings off the site.',
    { userId: { type: 'integer' }, active: { type: 'boolean' }, reason: note('Required') },
    ['userId', 'active', 'reason'],
  ),
];

export const ADMIN_TOOL_NAMES = new Set(ADMIN_TOOL_DEFINITIONS.map((t) => t.function.name));

/**
 * The admin's half of the assistant: the five queues of the admin panel.
 *
 * Every rule the admin screens enforce is enforced here before a confirm card is shown -
 * notes where a decision needs one, `canPublish` before an approval, inspection permission
 * before recording or granting, never switching off yourself or another admin - so the
 * admin is not asked to agree to something the API will only refuse.
 *
 * A phone number and CNIC appear on exactly one screen in the product, the admin user
 * detail (rule 116). They are not read into the chat at all: the conversation is kept in
 * session storage, and that shape must never be cached. The tool links to the screen.
 */
@Injectable({ providedIn: 'root' })
export class AdminToolbox {
  private readonly admin = inject(AdminService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  async prepare(name: string, args: Args): Promise<Prepared> {
    try {
      switch (name) {
        case 'admin_listings':
          return { outcome: await this.listings(args) };
        case 'admin_listing_details':
          return { outcome: await this.listing(args) };
        case 'admin_record_verification':
          return this.verification(args);
        case 'admin_decide_listing':
          return this.decideListing(args);
        case 'admin_revisions':
          return { outcome: await this.revisions(args) };
        case 'admin_revision_details':
          return { outcome: await this.revision(args) };
        case 'admin_decide_revision':
          return this.decideRevision(args);
        case 'admin_due_inspections':
          return { outcome: await this.dueInspections() };
        case 'admin_record_inspection':
          return this.recordInspection(args);
        case 'admin_change_badge':
          return this.badge(args);
        case 'admin_reviews':
          return { outcome: await this.reviews(args) };
        case 'admin_moderate_review':
          return this.moderate(args);
        case 'admin_reports':
          return { outcome: await this.reports(args) };
        case 'admin_resolve_report':
          return this.resolveReport(args);
        case 'admin_users':
          return { outcome: await this.users(args) };
        case 'admin_user_details':
          return { outcome: await this.user(args) };
        case 'admin_set_user_active':
          return this.setActive(args);
      }
      return { outcome: { result: { error: `There is no tool called ${name}.` } } };
    } catch (err) {
      return { outcome: toolFailure(err, this.router) };
    }
  }

  // ---------------------------------------------------------------- listings

  private async listings(args: Args): Promise<ToolOutcome> {
    const status = STATUSES.find((s) => s.toLowerCase() === String(args['status'] ?? '').toLowerCase());
    const page = await firstValueFrom(this.admin.properties(status));
    return {
      result: {
        filter: status ? statusLabel(status) : 'all',
        total: page.totalCount,
        listings: page.items.map((l) => ({
          propertyId: l.id,
          title: l.title,
          status: statusLabel(l.status),
          where: `${l.areaName}, ${l.cityName}`,
          owner: l.ownerName,
          rent: rupees(l.monthlyRent),
          photos: l.photoCount,
          updated: day(l.updatedAt),
        })),
      },
      links: [{ label: 'Open admin listings', commands: ['/admin/listings'] }],
    };
  }

  private async listing(args: Args): Promise<ToolOutcome> {
    const id = asInt(args['propertyId']);
    if (!id) return { result: { error: 'propertyId is required.' } };
    const l = await firstValueFrom(this.admin.property(id));
    return {
      result: {
        propertyId: l.id,
        title: l.title,
        status: statusLabel(l.status),
        canPublish: l.canPublish,
        missingForPublishing: missingChecks(l.verificationChecks),
        owner: { name: l.owner.fullName, identity: l.owner.identityStatus, identityCheckedOn: day(l.owner.identityCheckedAt) },
        where: `${l.addressLine}, ${l.areaName}, ${l.cityName}`,
        description: l.description,
        roomType: l.roomType,
        forWhom: l.genderPolicy,
        beds: `${l.availableBeds} of ${l.totalBeds} free`,
        rent: rupees(l.monthlyRent),
        deposit: rupees(l.securityDeposit),
        utilities: rupees(l.utilitiesCharge),
        mess: rupees(l.messCharge),
        houseRules: l.houseRules ?? 'none',
        facilities: l.facilities.map((f) => f.name),
        photos: l.photos.length,
        inspectionBadge: l.hasInspectionBadge ? 'Yes' : 'No',
        availabilityLastConfirmed: day(l.availabilityConfirmedAt),
        verificationChecks: l.verificationChecks.map((c) => ({
          check: c.checkType,
          status: c.status,
          evidenceDate: day(c.evidenceDate),
          note: c.note ?? 'none',
          recorded: day(c.recordedAt),
        })),
        created: day(l.createdAt),
        updated: day(l.updatedAt),
      },
      links: [{ label: 'Open admin listings', commands: ['/admin/listings'] }],
    };
  }

  private async verification(args: Args): Promise<Prepared> {
    const id = asInt(args['propertyId']);
    if (!id) return required('propertyId');
    const checkType = CHECK_TYPES.find((c) => c.toLowerCase() === String(args['checkType'] ?? '').toLowerCase());
    const status = (['Checked', 'Failed', 'NotChecked'] as VerificationStatus[]).find(
      (s) => s.toLowerCase() === String(args['status'] ?? '').replace(/\s/g, '').toLowerCase(),
    );
    if (!checkType) return { outcome: { result: { error: `checkType must be one of ${CHECK_TYPES.join(', ')}.` } } };
    if (!status) return { outcome: { result: { error: 'status must be Checked, Failed or NotChecked.' } } };
    const evidence = asText(args['evidenceDate']) ?? null;
    if (evidence && Number.isNaN(new Date(evidence).getTime())) return { outcome: { result: { error: 'evidenceDate is not a date.' } } };

    const l = await firstValueFrom(this.admin.property(id));
    const text = asText(args['note']) ?? null;
    return {
      pending: {
        summary: `Record ${checkType} as ${status} on "${l.title}"${evidence ? ` (evidence ${day(evidence)})` : ''}${text ? `: "${text}"` : ''}?`,
        run: () =>
          runWrite(
            this.admin.verify(id, { checkType: checkType as VerificationCheckType, status, evidenceDate: evidence, note: text }),
            { result: { ok: true, done: `${checkType} recorded as ${status}` } },
            this.router,
          ),
      },
    };
  }

  private async decideListing(args: Args): Promise<Prepared> {
    const id = asInt(args['propertyId']);
    if (!id) return required('propertyId');
    const action = (['approve', 'reject', 'request-changes', 'unpublish'] as const).find(
      (a) => a === String(args['action'] ?? '').toLowerCase().replace(/[\s_]/g, '-'),
    );
    if (!action) return { outcome: { result: { error: 'action must be approve, reject, request-changes or unpublish.' } } };
    const text = asText(args['note']) ?? '';
    if (action !== 'approve' && !text) {
      return { outcome: { result: { error: `A ${action} needs a note for the owner. Ask the admin what to write.` } } };
    }

    const l = await firstValueFrom(this.admin.property(id));
    if (action === 'approve' && !l.canPublish) {
      return {
        outcome: {
          result: { error: `It cannot be published yet. Still needed: ${missingChecks(l.verificationChecks).join(', ') || 'see the checks'}.` },
        },
      };
    }

    const verb = { approve: 'Approve and publish', reject: 'Reject', 'request-changes': 'Ask for changes to', unpublish: 'Unpublish' }[action];
    return {
      pending: {
        summary: `${verb} "${l.title}"${text ? `, telling the owner: "${text}"` : ''}?`,
        run: async () => {
          try {
            const after = await firstValueFrom(this.admin.decideProperty(id, action, text));
            return { result: { ok: true, done: `now ${statusLabel(after.status)}` } };
          } catch (err) {
            return toolFailure(err, this.router);
          }
        },
      },
    };
  }

  // ---------------------------------------------------------------- revisions

  private async revisions(args: Args): Promise<ToolOutcome> {
    const kind: RevisionKind = /avail/i.test(String(args['kind'] ?? '')) ? 'Availability' : 'Content';
    const page = await firstValueFrom(this.admin.revisions(kind));
    return {
      result: {
        kind,
        total: page.totalCount,
        revisions: page.items.map((r) => ({
          revisionId: r.id,
          listing: r.propertyTitle,
          propertyId: r.propertyId,
          where: `${r.areaName}, ${r.cityName}`,
          owner: r.ownerName,
          submitted: when(r.submittedAt),
          ...(kind === 'Availability'
            ? { beds: `${r.currentAvailableBeds} → ${r.proposedAvailableBeds} free of ${r.totalBeds}` }
            : { changedFields: r.changedFields }),
        })),
      },
      links: [{ label: `Open ${kind.toLowerCase()} revisions`, commands: ['/admin/revisions'], queryParams: { kind } }],
    };
  }

  private async revision(args: Args): Promise<ToolOutcome> {
    const id = asInt(args['revisionId']);
    if (!id) return { result: { error: 'revisionId is required.' } };
    const r = await firstValueFrom(this.admin.revision(id));
    return {
      result: {
        revisionId: r.id,
        listing: r.propertyTitle,
        kind: r.kind,
        status: r.reviewStatus,
        owner: r.ownerName,
        submitted: when(r.submittedAt),
        changes: r.changes.map((c) => `${c.field}: ${c.current ?? '(empty)'} → ${c.proposed ?? '(empty)'}`),
        photosAdded: r.photosToAdd.length,
        photosRemoved: r.photosToRemove.length,
        photoOrderChanged: !!r.proposedPhotoOrder,
        adminNote: r.adminNote ?? 'none',
      },
      links: [{ label: 'Open revisions', commands: ['/admin/revisions'], queryParams: { kind: r.kind } }],
    };
  }

  private async decideRevision(args: Args): Promise<Prepared> {
    const id = asInt(args['revisionId']);
    if (!id) return required('revisionId');
    const action = /^rej/i.test(String(args['action'] ?? '')) ? 'reject' : /^app/i.test(String(args['action'] ?? '')) ? 'approve' : null;
    if (!action) return { outcome: { result: { error: 'action must be approve or reject.' } } };
    const text = asText(args['note']) ?? '';
    if (action === 'reject' && !text) return { outcome: { result: { error: 'A rejection needs a note for the owner.' } } };

    const r = await firstValueFrom(this.admin.revision(id));
    if (r.reviewStatus !== 'Pending') return { outcome: { result: { error: `That revision is already ${r.reviewStatus}.` } } };
    const what = r.kind === 'Availability' ? 'bed count' : `${r.changes.length} change(s)`;
    return {
      pending: {
        summary:
          action === 'approve'
            ? `Approve the ${what} on "${r.propertyTitle}"? It goes live straight away.`
            : `Reject the ${what} on "${r.propertyTitle}", telling the owner: "${text}"?`,
        run: () =>
          runWrite(this.admin.decideRevision(id, action, text), { result: { ok: true, done: action === 'approve' ? 'revision approved and live' : 'revision rejected' } }, this.router),
      },
    };
  }

  // ---------------------------------------------------------------- inspections

  private async dueInspections(): Promise<ToolOutcome> {
    const page = await firstValueFrom(this.admin.dueInspections());
    return {
      result: {
        total: page.totalCount,
        due: page.items.map((d) => ({
          propertyId: d.propertyId,
          listing: d.title,
          where: `${d.areaName}, ${d.cityName}`,
          owner: d.ownerDisplayName,
          lastInspected: day(d.lastInspectionAt),
          daysOverdue: d.daysOverdue,
        })),
        youCanInspect: await this.canInspect(),
      },
      links: [{ label: 'Open inspections', commands: ['/admin/inspections'] }],
    };
  }

  private async recordInspection(args: Args): Promise<Prepared> {
    if (!(await this.canInspect())) {
      return { outcome: { result: { error: 'This admin account does not have inspection permission.' } } };
    }
    const id = asInt(args['propertyId']) ?? 0;
    const at = asText(args['inspectedAt']) ?? '';
    const problem = inspectionProblem(id, at);
    if (problem) return { outcome: { result: { error: problem } } };
    const result = /^fail/i.test(String(args['result'] ?? '')) ? 'Failed' : /^pass/i.test(String(args['result'] ?? '')) ? 'Passed' : null;
    if (!result) return { outcome: { result: { error: 'result must be Passed or Failed.' } } };
    const fee = asInt(args['feeAmount']) ?? null;
    const notes = asText(args['notes']) ?? null;

    const l = await firstValueFrom(this.admin.property(id));
    return {
      pending: {
        summary: `Record a ${result} inspection of "${l.title}" on ${when(at)}${fee ? `, fee ${rupees(fee)}` : ''}${notes ? `: "${notes}"` : ''}? The badge is a separate decision.`,
        run: () =>
          runWrite(
            this.admin.recordInspection({
              propertyId: id,
              inspectedAt: new Date(at).toISOString(),
              result,
              notes,
              feeAmount: fee,
              feeCollectedAt: fee ? new Date(at).toISOString() : null,
            }),
            { result: { ok: true, done: `inspection recorded as ${result}; granting the badge is a separate step` } },
            this.router,
          ),
      },
    };
  }

  private async badge(args: Args): Promise<Prepared> {
    const id = asInt(args['propertyId']);
    if (!id) return required('propertyId');
    const action = /^grant/i.test(String(args['action'] ?? '')) ? 'grant' : /^remov/i.test(String(args['action'] ?? '')) ? 'remove' : null;
    if (!action) return { outcome: { result: { error: 'action must be grant or remove.' } } };
    const reason = asText(args['reason']);
    if (!reason) return { outcome: { result: { error: 'A reason is required. Ask the admin for one.' } } };
    if (action === 'grant' && !(await this.canInspect())) {
      return { outcome: { result: { error: 'Only an admin with inspection permission can grant the badge. Any admin can remove it.' } } };
    }
    const l = await firstValueFrom(this.admin.property(id));
    if (action === 'grant' && l.hasInspectionBadge) return { outcome: { result: { error: 'It already has the badge.' } } };
    if (action === 'remove' && !l.hasInspectionBadge) return { outcome: { result: { error: 'It does not have the badge.' } } };
    return {
      pending: {
        summary: `${action === 'grant' ? 'Grant' : 'Remove'} the inspection badge ${action === 'grant' ? 'to' : 'from'} "${l.title}": "${reason}"?`,
        run: () => runWrite(this.admin.changeBadge(id, action, reason), { result: { ok: true, done: `badge ${action === 'grant' ? 'granted' : 'removed'}` } }, this.router),
      },
    };
  }

  /** Read from the admin's own account detail, as the inspections screen does. */
  private async canInspect(): Promise<boolean> {
    const me = this.auth.currentUser()?.id;
    if (!me) return false;
    try {
      return (await firstValueFrom(this.admin.user(me))).canInspect === true;
    } catch {
      return false;
    }
  }

  // ---------------------------------------------------------------- reviews and reports

  private async reviews(args: Args): Promise<ToolOutcome> {
    const asked = String(args['state'] ?? '').toLowerCase();
    const state = asked === 'all' ? undefined : ((['Pending', 'Approved', 'Hidden'] as ReviewState[]).find((s) => s.toLowerCase() === asked) ?? 'Pending');
    const page = await firstValueFrom(this.admin.reviews(state));
    return {
      result: {
        filter: state ?? 'all',
        total: page.totalCount,
        reviews: page.items.map((r) => ({
          reviewId: r.id,
          listing: r.propertyTitle,
          by: r.authorDisplayName,
          rating: r.rating,
          stay: r.stayStatus,
          comment: r.comment,
          state: r.moderationState,
          written: day(r.createdAt),
        })),
      },
      links: [{ label: 'Open reports and reviews', commands: ['/admin/reports'] }],
    };
  }

  private async moderate(args: Args): Promise<Prepared> {
    const id = asInt(args['reviewId']);
    if (!id) return required('reviewId');
    const state = /^app/i.test(String(args['state'] ?? '')) ? 'Approved' : /^hid/i.test(String(args['state'] ?? '')) ? 'Hidden' : null;
    if (!state) return { outcome: { result: { error: 'state must be Approved or Hidden.' } } };
    const all = await firstValueFrom(this.admin.reviews());
    const review = all.items.find((r) => r.id === id);
    if (!review) return { outcome: { result: { error: 'No review has that id. Call admin_reviews.' } } };
    if (review.moderationState === state) return { outcome: { result: { error: `It is already ${state}.` } } };
    return {
      pending: {
        summary: `${state === 'Approved' ? 'Approve and publish' : 'Hide'} ${review.authorDisplayName}'s ${review.rating}-star review of "${review.propertyTitle}"?`,
        run: () =>
          runWrite(this.admin.moderateReview(id, state), { result: { ok: true, done: state === 'Approved' ? 'review is now public' : 'review hidden from public pages' } }, this.router),
      },
    };
  }

  private async reports(args: Args): Promise<ToolOutcome> {
    const asked = String(args['status'] ?? '').replace(/\s/g, '').toLowerCase();
    const status = (['Open', 'UnderReview', 'Upheld', 'Dismissed'] as ReportStatus[]).find((s) => s.toLowerCase() === asked);
    const page = await firstValueFrom(this.admin.reports(status));
    // Asked for nothing in particular, an admin wants what still needs a decision.
    const unresolvedOnly = !status && asked !== 'all';
    const items = unresolvedOnly ? page.items.filter((r) => r.status === 'Open' || r.status === 'UnderReview') : page.items;
    return {
      result: {
        filter: status ?? (unresolvedOnly ? 'unresolved (Open and UnderReview)' : 'all'),
        total: items.length,
        reports: items.map((r) => ({
          reportId: r.id,
          listing: r.propertyTitle,
          propertyId: r.propertyId,
          reason: r.reason,
          details: r.details,
          by: r.reportedByDisplayName,
          status: r.status,
          adminNote: r.adminNote ?? 'none',
          filed: day(r.createdAt),
        })),
      },
      links: [{ label: 'Open reports and reviews', commands: ['/admin/reports'] }],
    };
  }

  private async resolveReport(args: Args): Promise<Prepared> {
    const id = asInt(args['reportId']);
    if (!id) return required('reportId');
    const status = (['UnderReview', 'Upheld', 'Dismissed'] as const).find(
      (s) => s.toLowerCase() === String(args['status'] ?? '').replace(/\s/g, '').toLowerCase(),
    );
    if (!status) return { outcome: { result: { error: 'status must be UnderReview, Upheld or Dismissed.' } } };
    const text = asText(args['note']) ?? '';
    if (status !== 'UnderReview' && !text) return { outcome: { result: { error: `${status} needs a note. Ask the admin what to write.` } } };

    const all = await firstValueFrom(this.admin.reports());
    const report = all.items.find((r) => r.id === id);
    if (!report) return { outcome: { result: { error: 'No report has that id. Call admin_reports.' } } };
    if (report.status === 'Upheld' || report.status === 'Dismissed') {
      return { outcome: { result: { error: `That report already has a verdict: ${report.status}.` } } };
    }
    return {
      pending: {
        summary:
          status === 'UnderReview'
            ? `Mark the report on "${report.propertyTitle}" as under review?`
            : `${status === 'Upheld' ? 'Uphold' : 'Dismiss'} the "${report.reason}" report on "${report.propertyTitle}": "${text}"?` +
              (status === 'Upheld' ? ' The listing stays public unless you unpublish it separately.' : ''),
        run: () =>
          runWrite(
            this.admin.resolveReport(id, status, text),
            {
              result: {
                ok: true,
                done:
                  status === 'Upheld'
                    ? 'report upheld - the listing is still public; unpublishing is a separate decision'
                    : `report moved to ${status}`,
              },
            },
            this.router,
          ),
      },
    };
  }

  // ---------------------------------------------------------------- users

  private async users(args: Args): Promise<ToolOutcome> {
    const role = (['Seeker', 'Owner', 'Admin'] as const).find((r) => r.toLowerCase() === String(args['role'] ?? '').toLowerCase());
    const isActive = typeof args['isActive'] === 'boolean' ? String(args['isActive']) : undefined;
    const q = asText(args['query']);
    const page = await firstValueFrom(this.admin.users({ role, isActive, q: q && q.length >= 2 ? q : undefined }));
    return {
      result: {
        total: page.totalCount,
        users: page.items.map((u) => ({
          userId: u.id,
          name: u.fullName,
          email: u.email,
          role: u.role,
          emailVerified: u.isEmailVerified ? 'Yes' : 'No',
          active: u.isActive ? 'Yes' : 'No',
          joined: day(u.createdAt),
        })),
      },
      links: [{ label: 'Open users', commands: ['/admin/users'] }],
    };
  }

  private async user(args: Args): Promise<ToolOutcome> {
    const id = asInt(args['userId']);
    if (!id) return { result: { error: 'userId is required.' } };
    const u = await firstValueFrom(this.admin.user(id));
    // Phone and CNIC are deliberately not copied out of this response - see the class note.
    return {
      result: {
        userId: u.id,
        name: u.fullName,
        email: u.email,
        role: u.role,
        emailVerified: u.isEmailVerified ? 'Yes' : 'No',
        active: u.isActive ? 'Yes' : 'No',
        joined: day(u.createdAt),
        identityCheck: u.identityStatus ?? 'not applicable',
        identityCheckedOn: day(u.identityCheckedAt),
        canInspect: u.role === 'Admin' ? (u.canInspect ? 'Yes' : 'No') : 'not applicable',
        phoneAndCnic: 'Shown only on the admin user detail screen, never in this chat. Point the admin there.',
        canBeSwitched: u.role !== 'Admin' && u.id !== this.auth.currentUser()?.id,
      },
      links: [{ label: 'Open users (phone and CNIC are there)', commands: ['/admin/users'] }],
    };
  }

  private async setActive(args: Args): Promise<Prepared> {
    const id = asInt(args['userId']);
    if (!id) return required('userId');
    if (typeof args['active'] !== 'boolean') return { outcome: { result: { error: 'active must be true or false.' } } };
    const active = args['active'];
    const reason = asText(args['reason']);
    if (!reason) return { outcome: { result: { error: 'A reason is required. Ask the admin for one.' } } };
    if (id === this.auth.currentUser()?.id) return { outcome: { result: { error: 'An admin cannot switch off their own account.' } } };

    const u = await firstValueFrom(this.admin.user(id));
    if (u.role === 'Admin') return { outcome: { result: { error: 'An admin account cannot be switched off from here.' } } };
    if (u.isActive === active) return { outcome: { result: { error: `${u.fullName} is already ${active ? 'active' : 'deactivated'}.` } } };

    const consequence = u.role === 'Owner' ? (active ? ' Their listings come back on the site.' : ' Their listings go off the public site.') : '';
    return {
      pending: {
        summary: `${active ? 'Reactivate' : 'Deactivate'} ${u.fullName} (${u.role}): "${reason}"?${consequence}`,
        run: () =>
          runWrite(this.admin.setUserActive(id, active, reason), { result: { ok: true, done: active ? 'account reactivated' : 'account deactivated' } }, this.router),
      },
    };
  }
}

/** The four checks that gate publishing, and which of them are not Checked yet. */
export function missingChecks(checks: { checkType: VerificationCheckType; status: VerificationStatus }[]): string[] {
  const gating: VerificationCheckType[] = ['OwnerIdentity', 'Address', 'Photos', 'Facilities'];
  return gating.filter((type) => checks.find((c) => c.checkType === type)?.status !== 'Checked');
}
