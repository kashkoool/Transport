import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  Bus,
  BusType,
  CancelTripResult,
  Company,
  Driver,
  PagedResult,
  Staff,
  StaffType,
  TripStop,
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
}
