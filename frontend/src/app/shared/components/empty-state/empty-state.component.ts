import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ButtonComponent } from '../button/button.component';

export type EmptyStateType = 'shortlist' | 'inbox' | 'visits' | 'search' | 'custom';

@Component({
  selector: 'app-empty-state',
  standalone: true,
  imports: [CommonModule, ButtonComponent],
  templateUrl: './empty-state.component.html',
  styleUrls: ['./empty-state.component.scss'],
})
export class EmptyStateComponent {
  @Input() type: EmptyStateType = 'custom';
  @Input() title: string = '';
  @Input() description: string = '';
  @Input() set message(val: string) {
    if (val) {
      this.description = val;
    }
  }
  @Input() actionText: string = '';
  @Input() actionVariant: 'primary' | 'success' | 'outline' = 'primary';
  @Input() secondaryActionText: string = '';

  @Output() actionClicked = new EventEmitter<void>();
  @Output() secondaryActionClicked = new EventEmitter<void>();

  get resolvedTitle(): string {
    if (this.title) return this.title;
    switch (this.type) {
      case 'shortlist':
        return 'No saved properties yet';
      case 'inbox':
        return 'Your inbox is empty';
      case 'visits':
        return 'No visit requests yet';
      case 'search':
        return 'No rooms match your filters';
      default:
        return 'Nothing to display';
    }
  }

  get resolvedDescription(): string {
    if (this.description) return this.description;
    switch (this.type) {
      case 'shortlist':
        return 'Properties you save while browsing will appear here so you can easily compare and review them.';
      case 'inbox':
        return 'When you contact an owner or request a room visit, your conversations will appear here.';
      case 'visits':
        return 'Schedule in-person viewings of verified properties. Your requested and confirmed visits will show up here.';
      case 'search':
        return 'Try widening your budget, selecting a broader area, or removing some facility filters.';
      default:
        return 'There is no data available at this time.';
    }
  }

  get resolvedActionText(): string {
    if (this.actionText) return this.actionText;
    switch (this.type) {
      case 'shortlist':
      case 'inbox':
      case 'visits':
        return 'Explore available rooms';
      case 'search':
        return 'Clear all filters';
      default:
        return '';
    }
  }
}
