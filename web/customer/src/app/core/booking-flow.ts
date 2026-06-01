import { Injectable, signal } from '@angular/core';
import { TripSummary } from './models';

/**
 * Carries the trip the customer picked from search into the booking page. The API has no
 * get-trip-by-id endpoint (search is the entry point), so the chosen trip is held here for the
 * duration of the linear search -> book flow. On a hard refresh this is empty and the booking
 * page sends the user back to search.
 */
@Injectable({ providedIn: 'root' })
export class BookingFlow {
  readonly trip = signal<TripSummary | null>(null);

  select(trip: TripSummary): void {
    this.trip.set(trip);
  }

  clear(): void {
    this.trip.set(null);
  }
}
