/**
 * API contracts — these mirror the backend DTOs exactly (camelCase, as ASP.NET serializes).
 * Keeping them in one place means a contract change surfaces as a compile error in the
 * services and components that use them.
 */

export interface AuthResult {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
  refreshTokenExpiresAtUtc: string;
  email: string;
  roles: string[];
}

export interface TokenResponse {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
  refreshTokenExpiresAtUtc: string;
}

export interface TripSummary {
  id: string;
  origin: string;
  destination: string;
  departureUtc: string;
  arrivalUtc: string;
  price: number;
  currency: string;
  seatCount: number;
  availableSeats: number;
}

export interface HoldResult {
  holdIds: string[];
  expiresAtUtc: string;
}

export interface PassengerInput {
  firstName: string;
  lastName: string;
  seatNumber: number;
  documentType?: string | null;
  documentNumber?: string | null;
}

/** Seating layout + currently-unavailable seats for a trip (public; seat numbers only). */
export interface SeatMap {
  tripId: string;
  seatCount: number;
  seatsPerRow: number;
  takenSeats: number[];
}

/** An ordered intermediate waypoint on a trip's route. */
export interface TripStop {
  sequence: number;
  name: string;
  arrivalUtc: string | null;
  departureUtc: string | null;
}

/** A promo code's effect on a trip's fare, previewed before booking. */
export interface PromoPreview {
  code: string;
  originalTotal: number;
  discount: number;
  total: number;
  currency: string;
}

export interface CancelBookingResult {
  status: string;
  refundInitiated: boolean;
}

export interface BookingResult {
  bookingId: string;
  reference: string;
  totalAmount: number;
  currency: string;
}

export interface CheckoutResult {
  checkoutUrl: string;
  gatewayReference: string;
}

export interface TicketPassenger {
  firstName: string;
  lastName: string;
  seatNumber: number;
}

export interface Ticket {
  bookingId: string;
  reference: string;
  status: string;
  origin: string;
  destination: string;
  departureUtc: string;
  totalAmount: number;
  currency: string;
  passengers: TicketPassenger[];
  qrPayload: string;
}

export interface BookingSummary {
  bookingId: string;
  reference: string;
  status: string;
  origin: string;
  destination: string;
  departureUtc: string;
  totalAmount: number;
  currency: string;
  createdAtUtc: string;
}

/** A single public review (display name only — no PII). */
export interface ReviewDto {
  id: string;
  rating: number;
  comment: string | null;
  displayName: string;
  createdAtUtc: string;
}

/** Average rating + count + the page of reviews for a trip or company. */
export interface ReviewSummary {
  averageRating: number;
  count: number;
  reviews: ReviewDto[];
}

/** RFC 7807 ProblemDetails as the API returns it, plus the stable `code` extension. */
export interface ProblemDetails {
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
}

// ── Admin + vendor consoles ──────────────────────────────────────────────────────

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  limit: number;
}

export type CompanyStatus = 'Pending' | 'Active' | 'Suspended';

export interface Company {
  id: string;
  name: string;
  email: string;
  phone: string | null;
  status: CompanyStatus;
}

export interface CompanyManager {
  userId: string;
  email: string;
  companyId: string;
}

export type BusType = 'Standard' | 'Premium' | 'Luxury' | 'Sleeper';

export interface Bus {
  id: string;
  busNumber: string;
  seatCount: number;
  type: BusType;
  model: string | null;
}

export type TripStatus = 'Scheduled' | 'InProgress' | 'Completed' | 'Cancelled';

export interface VendorTrip {
  id: string;
  busId: string;
  origin: string;
  destination: string;
  departureUtc: string;
  arrivalUtc: string;
  seatCount: number;
  price: number;
  currency: string;
  status: TripStatus;
}

export interface CancelTripResult {
  trip: VendorTrip;
  releasedHolds: number;
  cancelledPendingBookings: number;
  confirmedBookingsAffected: number;
}
