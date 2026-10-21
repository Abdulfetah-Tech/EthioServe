import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AdminCategory,
  AdminService,
  AdminServiceQuery,
  CategoryRequest,
  CreateServiceRequest,
  UpdateServiceRequest,
} from '../models/admin-catalog.models';
import { PagedResult } from '../models/marketplace.models';

@Injectable({ providedIn: 'root' })
export class AdminCatalogApi {
  private readonly http = inject(HttpClient);

  categories(): Observable<AdminCategory[]> {
    return this.http.get<AdminCategory[]>('/api/admin/categories');
  }

  createCategory(request: CategoryRequest): Observable<AdminCategory> {
    return this.http.post<AdminCategory>('/api/admin/categories', request);
  }

  updateCategory(id: string, request: CategoryRequest): Observable<AdminCategory> {
    return this.http.put<AdminCategory>(this.category(id), request);
  }

  setCategoryActive(id: string, active: boolean): Observable<AdminCategory> {
    return this.http.post<AdminCategory>(`${this.category(id)}/${active ? 'activate' : 'deactivate'}`, null);
  }

  services(query: AdminServiceQuery, pageSize = 15): Observable<PagedResult<AdminService>> {
    let params = new HttpParams().set('pageSize', String(pageSize));

    if (query.categoryId) {
      params = params.set('categoryId', query.categoryId);
    }

    if (query.search) {
      params = params.set('search', query.search);
    }

    if (query.includeInactive === false) {
      params = params.set('includeInactive', 'false');
    }

    if (query.page && query.page > 1) {
      params = params.set('page', String(query.page));
    }

    return this.http.get<PagedResult<AdminService>>('/api/admin/services', { params });
  }

  createService(request: CreateServiceRequest): Observable<AdminService> {
    return this.http.post<AdminService>('/api/admin/services', request);
  }

  updateService(id: string, request: UpdateServiceRequest): Observable<AdminService> {
    return this.http.put<AdminService>(this.service(id), request);
  }

  setServiceActive(id: string, active: boolean): Observable<AdminService> {
    return this.http.post<AdminService>(`${this.service(id)}/${active ? 'activate' : 'deactivate'}`, null);
  }

  private category(id: string): string {
    return `/api/admin/categories/${encodeURIComponent(id)}`;
  }

  private service(id: string): string {
    return `/api/admin/services/${encodeURIComponent(id)}`;
  }
}