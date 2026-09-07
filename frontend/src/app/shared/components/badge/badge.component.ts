import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export type BadgeVariant =
  | 'inspection'  // RoomRaah Inspection Badge
  | 'confirmed'   // Confirmed / Verified (#16A36A)
  | 'success'     // Success
  | 'navy'        // Primary (#123B5D)
  | 'warning'     // Pending / Warning (#d97706)
  | 'danger'      // Failed / Declined / Full (#dc2626)
  | 'neutral';    // Slate / Neutral

export type BadgeSize = 'sm' | 'md';

@Component({
  selector: 'app-badge',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './badge.component.html',
  styleUrls: ['./badge.component.scss'],
})
export class BadgeComponent {
  @Input() variant: BadgeVariant = 'neutral';
  @Input() size: BadgeSize = 'md';
  @Input() showDot: boolean = false;
  @Input() ariaLabel?: string;
}
