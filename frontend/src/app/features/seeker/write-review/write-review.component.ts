import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ButtonComponent, FormFieldComponent, InputDirective } from '../../../shared/components';
import { EngagementService } from '../../../core/services/engagement.service';
import { PropertyService } from '../../../core/services/property.service';
import { fieldErrorMap, toFailure } from '../../../core/http/api-error';
import { PropertyDetail, StayStatus } from '../../../core/models/catalog.model';
import { MyReview, REVIEW_COMMENT_MAX, STAY_STATUSES } from '../../../core/models/engagement.model';

/**
 * Writing a review of one listing.
 *
 * Two things this screen must get right. A review can only be written after a visit to this
 * listing reached Completed, and only once - both are 422s, and both come back as sentences
 * written to be read, so they are shown exactly as sent rather than replaced with a house
 * apology that explains nothing.
 *
 * The other is the success screen. A new review is created Pending and is invisible until an
 * admin approves it. If nobody says so, the author refreshes the listing, cannot find their
 * review, and writes it again.
 */
@Component({
  selector: 'app-write-review',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, ButtonComponent, FormFieldComponent, InputDirective],
  templateUrl: './write-review.component.html',
  styleUrls: ['./write-review.component.scss'],
})
export class WriteReviewComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly properties = inject(PropertyService);
  private readonly engagement = inject(EngagementService);

  readonly propertyId = signal(0);
  readonly listing = signal<PropertyDetail | null>(null);

  readonly rating = signal(5);
  readonly comment = signal('');
  readonly stayStatus = signal<StayStatus>('Visited');

  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);
  readonly fieldErrors = signal<Record<string, string>>({});
  readonly created = signal<MyReview | null>(null);

  readonly stayOptions = STAY_STATUSES;
  readonly commentMax = REVIEW_COMMENT_MAX;
  readonly stars = [1, 2, 3, 4, 5];

  constructor() {
    this.route.paramMap.subscribe((params) => {
      const id = Number(params.get('id'));
      this.propertyId.set(Number.isFinite(id) ? id : 0);
      if (id > 0) {
        this.properties.detail(id).subscribe({
          next: (listing) => this.listing.set(listing),
          error: () => this.listing.set(null),
        });
      }
    });
  }

  submit(): void {
    const comment = this.comment().trim();
    if (!comment) {
      this.error.set('Write a sentence or two about the room before sending this.');
      return;
    }

    this.submitting.set(true);
    this.error.set(null);
    this.fieldErrors.set({});

    this.engagement
      .writeReview({
        propertyId: this.propertyId(),
        rating: this.rating(),
        comment,
        stayStatus: this.stayStatus(),
      })
      .subscribe({
        next: (review) => {
          this.created.set(review);
          this.submitting.set(false);
        },
        error: (err) => {
          const failure = toFailure(err);
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
