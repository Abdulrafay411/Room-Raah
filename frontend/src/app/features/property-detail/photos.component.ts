import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { PropertyService } from '../../core/services/property.service';
import { toFailure } from '../../core/http/api-error';
import { PropertyPhoto } from '../../core/models/catalog.model';

/** The full gallery, on its own route so it can be linked to and comes back on Back. */
@Component({
  selector: 'app-property-photos',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './photos.component.html',
  styleUrls: ['./photos.component.scss'],
})
export class PropertyPhotosComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly properties = inject(PropertyService);

  readonly photos = signal<PropertyPhoto[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly propertyId = signal(0);

  constructor() {
    this.route.paramMap.subscribe((params) => {
      const id = Number(params.get('id'));
      this.propertyId.set(id);
      this.loading.set(true);
      this.error.set(null);

      this.properties.photos(id).subscribe({
        next: (photos) => {
          // Primary first, then the owner's ordering - the same order the listing shows.
          this.photos.set(
            [...(photos ?? [])].sort(
              (a, b) => Number(b.isPrimary) - Number(a.isPrimary) || a.sortOrder - b.sortOrder,
            ),
          );
          this.loading.set(false);
        },
        error: (err) => {
          this.error.set(toFailure(err).message);
          this.loading.set(false);
        },
      });
    });
  }
}
