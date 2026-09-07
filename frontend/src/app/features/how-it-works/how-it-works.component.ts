import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

/**
 * What the product does, in the order somebody meets it. No API calls: the point of this
 * page is to answer the questions that otherwise get asked as support messages - who
 * checked this listing, why there is no telephone number, and how a review comes about.
 */
@Component({
  selector: 'app-how-it-works',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './how-it-works.component.html',
  styleUrls: ['./how-it-works.component.scss'],
})
export class HowItWorksComponent {}
