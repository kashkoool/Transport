import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  BookingResult,
  BookingSummary,
  CheckoutResult,
  HoldResult,
  PassengerInput,
  Ticket,
  TripSummary,
} from '../models';

/** Typed wrappers over the customer API. The auth/error interceptors handle tokens + failures. */
@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBaseUrl;

  searchTrips(origin: string, destination: string, date: string): Observable<TripSummary[]> {
    const params = new HttpParams()
      .set('origin', origin)
      .set('destination', destination)
      .set('date', date);
    return this.http.get<TripSummary[]>(`${this.base}/trips/search`, { params });
  }

  holdSeats(tripId: string, seatNumbers: number[]): Observable<HoldResult> {
    return this.http.post<HoldResult>(`${this.base}/bookings/hold`, { tripId, seatNumbers });
  }

  createBooking(tripId: string, passengers: PassengerInput[]): Observable<BookingResult> {
    // A fresh idempotency key per attempt makes a retried POST safe — the backend dedupes on it.
    const headers = { 'Idempotency-Key': crypto.randomUUID() };
    return this.http.post<BookingResult>(`${this.base}/bookings`, { tripId, passengers }, { headers });
  }

  myBookings(): Observable<BookingSummary[]> {
    return this.http.get<BookingSummary[]>(`${this.base}/bookings`);
  }

  ticket(bookingId: string): Observable<Ticket> {
    return this.http.get<Ticket>(`${this.base}/bookings/${bookingId}/ticket`);
  }

  checkout(bookingId: string): Observable<CheckoutResult> {
    return this.http.post<CheckoutResult>(`${this.base}/payments/checkout`, { bookingId });
  }

  /** DEV ONLY: ask the backend to simulate the gateway posting a signed payment result. */
  simulatePayment(bookingReference: string, succeeded: boolean): Observable<{ status: string }> {
    return this.http.post<{ status: string }>(`${this.base}/payments/dev/simulate`, {
      bookingReference,
      succeeded,
    });
  }
}
