import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Bus, BusType, CancelTripResult, PagedResult, VendorTrip } from '../models';

export interface AddBusRequest {
  busNumber: string;
  seatCount: number;
  type: BusType;
  model: string | null;
}

export interface ScheduleTripRequest {
  busId: string;
  origin: string;
  destination: string;
  departureUtc: string;
  arrivalUtc: string;
  price: number;
  currency: string;
}

/** Vendor-scoped API. Every call is auto-scoped to the caller's company by the backend. */
@Injectable({ providedIn: 'root' })
export class VendorApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/vendor`;

  listBuses(page = 1, limit = 20): Observable<PagedResult<Bus>> {
    const params = new HttpParams().set('page', page).set('limit', limit);
    return this.http.get<PagedResult<Bus>>(`${this.base}/buses`, { params });
  }

  addBus(body: AddBusRequest): Observable<Bus> {
    return this.http.post<Bus>(`${this.base}/buses`, body);
  }

  listTrips(page = 1, limit = 20): Observable<PagedResult<VendorTrip>> {
    const params = new HttpParams().set('page', page).set('limit', limit);
    return this.http.get<PagedResult<VendorTrip>>(`${this.base}/trips`, { params });
  }

  scheduleTrip(body: ScheduleTripRequest): Observable<VendorTrip> {
    return this.http.post<VendorTrip>(`${this.base}/trips`, body);
  }

  cancelTrip(tripId: string): Observable<CancelTripResult> {
    return this.http.post<CancelTripResult>(`${this.base}/trips/${tripId}/cancel`, {});
  }
}
