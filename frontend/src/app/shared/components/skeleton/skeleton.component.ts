import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-skeleton',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './skeleton.component.html',
  styleUrls: ['./skeleton.component.scss'],
})
export class SkeletonComponent {
  @Input() variant: 'text' | 'rectangular' | 'circular' = 'text';
  @Input() width: string = '100%';
  @Input() height: string = '';
  @Input() borderRadius: string = '';
}
