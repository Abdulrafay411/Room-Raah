import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ButtonComponent, FormFieldComponent, InputDirective } from '../../../shared/components';
import { EngagementService } from '../../../core/services/engagement.service';
import { PropertyService } from '../../../core/services/property.service';
import { fieldErrorMap, toFailure } from '../../../core/http/api-error';
import { PropertyDetail } from '../../../core/models/catalog.model';
import {
  VISIT_TYPES,
  VisitRequest,
  VisitType,
  toLocalInputValue,
  visitProblem,
  visitWindow,
} from '../../../core/models/engagement.model';

/**
 * Page 15: ask the owner of one listing for a visit.
 *
 * The date is the thing that catches people out. `preferredAt` must be in the future and at
 * most sixty days ahead, and either side of that is a 400 - so the picker is given `min` and
 * `max` and cannot produce the wrong answer, and the same window is checked again before
 * anything is sent. Both, not one: the attributes are advisory in some browsers.
 *
 * This is also the one screen where somebody is deliberately reaching an owner, which is
 * exactly why no telephone number or email appears on it. The request is the channel.
 */
@Component({
  selector: 'app-visit-request',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, ButtonComponent, FormFieldComponent, InputDirective],
  templateUrl: './visit-request.component.html',
  styleUrls: ['./visit-request.component.scss'],
})
export class VisitRequestComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly properties = inject(PropertyService);
  private readonly engagement = inject(EngagementService);

  readonly propertyId = signal(0);
  readonly listing = signal<PropertyDetail | null>(null);
  readonly loading = signal(true);

  readonly visitType = signal<VisitType>('Physical');
  readonly preferredAt = signal('');

  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);
  readonly fieldErrors = signal<Record<string, string>>({});
  readonly created = signal<VisitRequest | null>(null);

  readonly types = VISIT_TYPES;
  readonly window = visitWindow();

  /** Shown under the field as soon as it is wrong, rather than only on submit. */
  readonly dateProblem = computed(() =>
    this.preferredAt() ? visitProblem(this.preferredAt()) : null,
  );

  constructor() {
    // Tomorrow at the same hour: a default somebody is likely to keep, and always inside
    // the window the server accepts.
    const tomorrow = new Date();
    tomorrow.setDate(tomorrow.getDate() + 1);
    this.preferredAt.set(toLocalInputValue(tomorrow));

    this.route.paramMap.subscribe((params) => {
      const id = Number(params.get('id'));
      this.propertyId.set(Number.isFinite(id) ? id : 0);
      this.loadListing(id);
    });
  }

  private loadListing(id: number): void {
    if (!Number.isFinite(id) || id <= 0) {
      this.loading.set(false);
      this.error.set('That listing does not exist.');
      return;
    }

    this.properties.detail(id).subscribe({
      next: (listing) => {
        this.listing.set(listing);
        this.loading.set(false);
      },
      // The form is still usable without the heading: the request only needs the id from
      // the URL. Losing the title costs context, not the feature.
      error: () => this.loading.set(false),
    });
  }

  submit(): void {
    const problem = visitProblem(this.preferredAt());
    if (problem) {
      this.error.set(problem);
      return;
    }

    this.submitting.set(true);
    this.error.set(null);
    this.fieldErrors.set({});

    this.engagement
      .requestVisit({
        propertyId: this.propertyId(),
        visitType: this.visitType(),
        // The picker speaks local time and the API takes an ISO instant; sending the raw
        // field value would send whatever the browser's clock happens to be as if it were UTC.
        preferredAt: new Date(this.preferredAt()).toISOString(),
      })
      .subscribe({
        next: (visit) => {
          this.created.set(visit);
          this.submitting.set(false);
        },
        error: (err) => {
          const failure = toFailure(err);
          // A 403 for an unverified account is a different screen, not a message.
          if (failure.needsVerification) {
            this.router.navigate(['/verify']);
            return;
          }
          this.error.set(failure.message);
          this.fieldErrors.set(fieldErrorMap(failure));
          this.submitting.set(false);
        },
      });
  }
}
