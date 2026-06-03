import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AdminCompanyReportRow,
  AdminSystemSummary,
  Company,
  CompanyManager,
  CompanyStatus,
  CustomerAccount,
  PagedResult,
} from '../models';

export interface CreateCompanyRequest {
  name: string;
  email: string;
  phone: string | null;
}

export interface CreateManagerRequest {
  email: string;
  password: string;
  fullName: string;
}

/** Admin-only API: company onboarding, activation/suspension, and vendor-manager creation. */
@Injectable({ providedIn: 'root' })
export class AdminApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/admin`;

  listCompanies(page = 1, limit = 20, status?: CompanyStatus): Observable<PagedResult<Company>> {
    let params = new HttpParams().set('page', page).set('limit', limit);
    if (status) params = params.set('status', status);
    return this.http.get<PagedResult<Company>>(`${this.base}/companies`, { params });
  }

  createCompany(body: CreateCompanyRequest): Observable<Company> {
    return this.http.post<Company>(`${this.base}/companies`, body);
  }

  activateCompany(id: string): Observable<Company> {
    return this.http.post<Company>(`${this.base}/companies/${id}/activate`, {});
  }

  suspendCompany(id: string): Observable<Company> {
    return this.http.post<Company>(`${this.base}/companies/${id}/suspend`, {});
  }

  createManager(companyId: string, body: CreateManagerRequest): Observable<CompanyManager> {
    return this.http.post<CompanyManager>(`${this.base}/companies/${companyId}/manager`, body);
  }

  /** Send an in-app notification to a company's manager(s). */
  notifyCompany(
    companyId: string,
    body: { title: string; message: string; type: string | null },
  ): Observable<unknown> {
    return this.http.post(`${this.base}/companies/${companyId}/notify`, body);
  }

  systemSummary(): Observable<AdminSystemSummary> {
    return this.http.get<AdminSystemSummary>(`${this.base}/reports/summary`);
  }

  companyReport(): Observable<AdminCompanyReportRow[]> {
    return this.http.get<AdminCompanyReportRow[]>(`${this.base}/reports/companies`);
  }

  // ── Customer accounts (management only) ──────────────────────────────────────────
  listCustomers(page = 1, limit = 20, search?: string): Observable<PagedResult<CustomerAccount>> {
    let params = new HttpParams().set('page', page).set('limit', limit);
    if (search) params = params.set('search', search);
    return this.http.get<PagedResult<CustomerAccount>>(`${this.base}/users`, { params });
  }

  suspendCustomer(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/users/${id}/suspend`, {});
  }

  reactivateCustomer(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/users/${id}/reactivate`, {});
  }

  deleteCustomer(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/users/${id}`);
  }
}
