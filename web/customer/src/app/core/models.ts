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

/** RFC 7807 ProblemDetails as the API returns it, plus the stable `code` extension. */
export interface ProblemDetails {
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
}
