import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { EmptyStateComponent } from '../../shared/components';

@Component({
  selector: 'app-not-found',
  standalone: true,
  imports: [RouterLink, EmptyStateComponent],
  template: `
    <div class="container-centered not-found-page">
      <h1 class="sr-only">Page not found</h1>
      <app-empty-state
        title="Page not found"
        description="The page you are looking for does not exist or has been moved."
        icon="search"
      >
        <a routerLink="/" class="home-link">Return home</a>
      </app-empty-state>
    </div>
  `,
  styles: [
    `
      .not-found-page {
        padding: 4rem 1rem;
        display: flex;
        justify-content: center;
        align-items: center;
      }

      .home-link {
        display: inline-flex;
        min-height: 40px;
        align-items: center;
        padding: 0.6rem 1rem;
        border-radius: 8px;
        background: #123b5d;
        color: #fff;
        font-weight: 600;
        text-decoration: none;
      }
    `,
  ],
})
export class NotFoundComponent {}
