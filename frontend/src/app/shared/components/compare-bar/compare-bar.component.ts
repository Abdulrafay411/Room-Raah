import { Component, Input, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { CompareStore } from '../../../core/services/compare.store';

/**
 * The floating "X of 3 chosen" bar. It only exists once something is ticked, which is also
 * what tells somebody that ticking did anything at all. Shared with the listing page, which
 * ticks rooms the same way search does.
 */
@Component({
  selector: 'app-compare-bar',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './compare-bar.component.html',
  styleUrls: ['./compare-bar.component.scss'],
})
export class CompareBarComponent {
  readonly compare = inject(CompareStore);

  /** Carried into the `/compare` link so a landmark picked on this screen survives the jump. */
  @Input() landmarkId: number | undefined;
}
