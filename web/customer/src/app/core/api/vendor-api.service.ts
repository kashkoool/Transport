import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  Bus,
  BusType,
  BookingResult,
  CancelBookingResult,
  CancelTripResult,
  Company,
  CompanyBooking,
  DemandPrediction,
  DiscountType,
  Driver,
  PagedResult,
  PassengerInput,
  PromoCodeDto,
  Staff,
  StaffType,
  TripReportRow,
  TripStop,
  VendorReportSummary,
  VendorTrip,
} from '../models';

export interface AddBusRequest {
  busNumber: string;
  seatCount: number;
  type: BusType;
  model: string | null;
  seatsPerRow: number;
}

export type UpdateBusRequest = Omit<AddBusRequest, 'busNumber'>;

export interface ScheduleTripRequest {
  busId: string;
  origin: string;
  destination: string;
  departureUtc: string;
  arrivalUtc: string;
  price: number;
  currency: string;
}

export type UpdateTripRequest = Omit<ScheduleTripRequest, 'busId'>;

export interface TripStopInput {
  name: string;
  arrivalUtc: string | null;
  departureUtc: string | null;
}

export interface CreateStaffRequest {
  email: string;
  password: string;
  fullName: string;
  staffType: StaffType;
}

export interface AddDriverRequest {
  fullName: string;
  phone: string | null;
  licenseNumber: string | null;
}

export interface CreatePromoCodeRequest {
  code: string;
  discountType: DiscountType;
  discountValue: number;
  maxRedemptions: number | null;
  expiresAtUtc: string | null;
}

export interface CounterBookingRequest {
  tripId: string;
  customerEmail: string;
  passengers: PassengerInput[];
}

export type ReportFormat = 'csv' | 'xlsx' | 'pdf';

/** Vendor-scoped API. Every call is auto-scoped to the caller's company by the backend. */
@Injectable({ providedIn: 'root' })
export class VendorApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/vendor`;

  // ── Fleet ──────────────────────────────────────────────────────────────────────
  listBuses(page = 1, limit = 20): Observable<PagedResult<Bus>> {
    const params = new HttpParams().set('page', page).set('limit', limit);
    return this.http.get<PagedResult<Bus>>(`${this.base}/buses`, { params });
  }

  addBus(body: AddBusRequest): Observable<Bus> {
    return this.http.post<Bus>(`${this.base}/buses`, body);
  }

  updateBus(busId: string, body: UpdateBusRequest): Observable<Bus> {
    return this.http.put<Bus>(`${this.base}/buses/${busId}`, body);
  }

  deleteBus(busId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/buses/${busId}`);
  }

  assignDriver(busId: string, driverId: string | null): Observable<Bus> {
    return this.http.post<Bus>(`${this.base}/buses/${busId}/driver`, { driverId });
  }

  // ── Trips ──────────────────────────────────────────────────────────────────────
  listTrips(page = 1, limit = 20): Observable<PagedResult<VendorTrip>> {
    const params = new HttpParams().set('page', page).set('limit', limit);
    return this.http.get<PagedResult<VendorTrip>>(`${this.base}/trips`, { params });
  }

  scheduleTrip(body: ScheduleTripRequest): Observable<VendorTrip> {
    return this.http.post<VendorTrip>(`${this.base}/trips`, body);
  }

  updateTrip(tripId: string, body: UpdateTripRequest): Observable<VendorTrip> {
    return this.http.put<VendorTrip>(`${this.base}/trips/${tripId}`, body);
  }

  deleteTrip(tripId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/trips/${tripId}`);
  }

  cancelTrip(tripId: string): Observable<CancelTripResult> {
    return this.http.post<CancelTripResult>(`${this.base}/trips/${tripId}/cancel`, {});
  }

  startTrip(tripId: string): Observable<VendorTrip> {
    return this.http.post<VendorTrip>(`${this.base}/trips/${tripId}/start`, {});
  }

  completeTrip(tripId: string): Observable<VendorTrip> {
    return this.http.post<VendorTrip>(`${this.base}/trips/${tripId}/complete`, {});
  }

  revertTrip(tripId: string): Observable<VendorTrip> {
    return this.http.post<VendorTrip>(`${this.base}/trips/${tripId}/revert`, {});
  }

  setTripStops(tripId: string, stops: TripStopInput[]): Observable<TripStop[]> {
    return this.http.put<TripStop[]>(`${this.base}/trips/${tripId}/stops`, { stops });
  }

  // ── Staff ──────────────────────────────────────────────────────────────────────
  listStaff(page = 1, limit = 50): Observable<PagedResult<Staff>> {
    const params = new HttpParams().set('page', page).set('limit', limit);
    return this.http.get<PagedResult<Staff>>(`${this.base}/staff`, { params });
  }

  createStaff(body: CreateStaffRequest): Observable<Staff> {
    return this.http.post<Staff>(`${this.base}/staff`, body);
  }

  suspendStaff(staffId: string): Observable<void> {
    return this.http.post<void>(`${this.base}/staff/${staffId}/suspend`, {});
  }

  reactivateStaff(staffId: string): Observable<void> {
    return this.http.post<void>(`${this.base}/staff/${staffId}/reactivate`, {});
  }

  // ── Drivers ────────────────────────────────────────────────────────────────────
  listDrivers(page = 1, limit = 100): Observable<PagedResult<Driver>> {
    const params = new HttpParams().set('page', page).set('limit', limit);
    return this.http.get<PagedResult<Driver>>(`${this.base}/drivers`, { params });
  }

  addDriver(body: AddDriverRequest): Observable<Driver> {
    return this.http.post<Driver>(`${this.base}/drivers`, body);
  }

  // ── Company profile ──────────────────────────────────────────────────────────────
  getCompany(): Observable<Company> {
    return this.http.get<Company>(`${this.base}/company`);
  }

  updateCompany(body: { name: string; phone: string | null }): Observable<Company> {
    return this.http.put<Company>(`${this.base}/company`, body);
  }

  // ── Reports + demand ─────────────────────────────────────────────────────────────
  reportSummary(from?: string, to?: string): Observable<VendorReportSummary> {
    return this.http.get<VendorReportSummary>(`${this.base}/reports/summary`, { params: range(from, to) });
  }

  tripReport(from?: string, to?: string): Observable<TripReportRow[]> {
    return this.http.get<TripReportRow[]>(`${this.base}/reports/trips`, { params: range(from, to) });
  }

  /** Download the per-trip report as a file (csv/xlsx/pdf). */
  exportTripReport(format: ReportFormat, from?: string, to?: string): Observable<Blob> {
    const params = range(from, to).set('format', format);
    return this.http.get(`${this.base}/reports/trips/export`, { params, responseType: 'blob' });
  }

  predictDemand(origin: string, destination: string, date: string): Observable<DemandPrediction> {
    const params = new HttpParams().set('origin', origin).set('destination', destination).set('date', date);
    return this.http.get<DemandPrediction>(`${this.base}/demand/predict`, { params });
  }

  // ── Promo codes ──────────────────────────────────────────────────────────────────
  listPromoCodes(page = 1, limit = 50): Observable<PagedResult<PromoCodeDto>> {
    const params = new HttpParams().set('page', page).set('limit', limit);
    return this.http.get<PagedResult<PromoCodeDto>>(`${this.base}/promo-codes`, { params });
  }

  createPromoCode(body: CreatePromoCodeRequest): Observable<PromoCodeDto> {
    return this.http.post<PromoCodeDto>(`${this.base}/promo-codes`, body);
  }

  deactivatePromoCode(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/promo-codes/${id}/deactivate`, {});
  }

  // ── Desk (counter booking) ───────────────────────────────────────────────────────
  counterBooking(body: CounterBookingRequest): Observable<BookingResult> {
    return this.http.post<BookingResult>(`${this.base}/bookings`, body);
  }

  listCompanyBookings(page = 1, limit = 20): Observable<PagedResult<CompanyBooking>> {
    const params = new HttpParams().set('page', page).set('limit', limit);
    return this.http.get<PagedResult<CompanyBooking>>(`${this.base}/bookings`, { params });
  }

  cancelCompanyBooking(bookingId: string): Observable<CancelBookingResult> {
    return this.http.post<CancelBookingResult>(`${this.base}/bookings/${bookingId}/cancel`, {});
  }
}

/** Build the optional from/to date-range query params shared by the report endpoints. */
function range(from?: string, to?: string): HttpParams {
  let params = new HttpParams();
  if (from) params = params.set('from', from);
  if (to) params = params.set('to', to);
  return params;
}
