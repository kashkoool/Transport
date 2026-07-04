import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  BookingResult,
  BookingSummary,
  CancelBookingResult,
  CheckoutResult,
  HoldResult,
  PassengerInput,
  PromoPreview,
  PublicCompany,
  ReviewDto,
  ReviewSummary,
  SeatMap,
  Ticket,
  TripStop,
  TripSummary,
} from '../models';

/** Typed wrappers over the customer API. The auth/error interceptors handle tokens + failures. */
@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBaseUrl;

  searchTrips(
    origin: string,
    destination: string,
    date: string,
    filters?: { companyId?: string; maxPrice?: number; departAfter?: string },
  ): Observable<TripSummary[]> {
    let params = new HttpParams()
      .set('origin', origin)
      .set('destination', destination)
      .set('date', date);
    if (filters?.companyId) params = params.set('companyId', filters.companyId);
    if (filters?.maxPrice != null) params = params.set('maxPrice', filters.maxPrice);
    if (filters?.departAfter) params = params.set('departAfter', filters.departAfter);
    return this.http.get<TripSummary[]>(`${this.base}/trips/search`, { params });
  }

  /** Active companies (id + name) for the search "filter by company" control. */
  companies(): Observable<PublicCompany[]> {
    return this.http.get<PublicCompany[]>(`${this.base}/companies`);
  }

  /** Seating layout + taken seats for the seat picker (public). */
  seatMap(tripId: string): Observable<SeatMap> {
    return this.http.get<SeatMap>(`${this.base}/trips/${tripId}/seat-map`);
  }

  /** Ordered intermediate stops for a trip (public). */
  tripStops(tripId: string): Observable<TripStop[]> {
    return this.http.get<TripStop[]>(`${this.base}/trips/${tripId}/stops`);
  }

  holdSeats(tripId: string, seatNumbers: number[]): Observable<HoldResult> {
    return this.http.post<HoldResult>(`${this.base}/bookings/hold`, { tripId, seatNumbers });
  }

  createBooking(
    tripId: string,
    passengers: PassengerInput[],
    promoCode?: string | null,
    contactPhone?: string | null,
  ): Observable<BookingResult> {
    // A fresh idempotency key per attempt makes a retried POST safe — the backend dedupes on it.
    const headers = { 'Idempotency-Key': crypto.randomUUID() };
    return this.http.post<BookingResult>(
      `${this.base}/bookings`,
      { tripId, passengers, promoCode: promoCode || null, contactPhone: contactPhone || null },
      { headers },
    );
  }

  /** Preview a promo code's discount on a trip before booking. */
  previewPromo(tripId: string, code: string, seats: number): Observable<PromoPreview> {
    const params = new HttpParams()
      .set('tripId', tripId)
      .set('code', code)
      .set('seats', seats);
    return this.http.get<PromoPreview>(`${this.base}/bookings/promo-preview`, { params });
  }

  cancelBooking(bookingId: string): Observable<CancelBookingResult> {
    return this.http.post<CancelBookingResult>(`${this.base}/bookings/${bookingId}/cancel`, {});
  }

  /** Public reviews + average rating for a trip. */
  tripReviews(tripId: string, page = 1, limit = 5): Observable<ReviewSummary> {
    const params = new HttpParams().set('page', page).set('limit', limit);
    return this.http.get<ReviewSummary>(`${this.base}/trips/${tripId}/reviews`, { params });
  }

  /** Rate a trip you travelled on (1–5 + optional comment). */
  createReview(bookingId: string, rating: number, comment: string | null): Observable<ReviewDto> {
    return this.http.post<ReviewDto>(`${this.base}/reviews`, { bookingId, rating, comment });
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
