import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

/** One row in the /api/seo/routes index (a bookable origin→destination pair). */
export interface SeoRoute {
  origin: string;
  destination: string;
  slug: string;
  tripCount: number;
  minPrice: number;
  currency: string;
}

/** A single upcoming departure shown on a route page. */
export interface SeoRouteDeparture {
  departureUtc: string;
  arrivalUtc: string;
  price: number;
  currency: string;
  companyName: string;
}

/** Full detail for one route slug (the money page). */
export interface SeoRouteDetail {
  origin: string;
  destination: string;
  slug: string;
  minPrice: number;
  currency: string;
  upcomingCount: number;
  companies: string[];
  avgDurationMinutes: number | null;
  next: SeoRouteDeparture[];
}

/** A city that appears as an origin or destination on at least one route. */
export interface SeoCity {
  name: string;
  slug: string;
  routeCount: number;
}

/** Typed client for the backend SEO API (public, unauthenticated). */
@Injectable({ providedIn: 'root' })
export class SeoApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/seo`;

  routes(): Observable<SeoRoute[]> {
    return this.http.get<SeoRoute[]>(`${this.base}/routes`);
  }

  route(slug: string): Observable<SeoRouteDetail> {
    return this.http.get<SeoRouteDetail>(`${this.base}/routes/${encodeURIComponent(slug)}`);
  }

  cities(): Observable<SeoCity[]> {
    return this.http.get<SeoCity[]>(`${this.base}/cities`);
  }
}
