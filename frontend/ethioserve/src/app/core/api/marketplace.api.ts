import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  Category,
  City,
  PagedResult,
  ProviderSearchParams,
  ProviderSummary,
  PublicProvider,
  Region,
  ServiceSummary,
  SubCity,
} from '../models/marketplace.models';

type ParamValue = string | number | boolean | undefined;

/** Only values that are actually set reach the URL. */
function buildParams(values: Record<string, ParamValue>): HttpParams {
  let params = new HttpParams();

  for (const [key, value] of Object.entries(values)) {
    if (value !== undefined && value !== '') {
      params = params.set(key, String(value));
    }
  }

  return params;
}

export interface ServiceQuery {
  categoryId?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}

@Injectable({ providedIn: 'root' })
export class MarketplaceApi {
  private readonly http = inject(HttpClient);

  categories(): Observable<Category[]> {
    return this.http.get<Category[]>('/api/categories');
  }

  services(query: ServiceQuery = {}): Observable<PagedResult<ServiceSummary>> {
    return this.http.get<PagedResult<ServiceSummary>>('/api/services', { params: buildParams({ ...query }) });
  }

  searchProviders(params: ProviderSearchParams, pageSize = 12): Observable<PagedResult<ProviderSummary>> {
    return this.http.get<PagedResult<ProviderSummary>>('/api/providers', {
      params: buildParams({
        search: params.search,
        categoryId: params.categoryId,
        serviceId: params.serviceId,
        regionId: params.regionId,
        cityId: params.cityId,
        minRating: params.minRating,
        availableOnly: params.availableOnly ? true : undefined,
        sort: params.sort,
        page: params.page,
        pageSize,
      }),
    });
  }

  provider(id: string): Observable<PublicProvider> {
    return this.http.get<PublicProvider>(`/api/providers/${encodeURIComponent(id)}`);
  }

  regions(): Observable<Region[]> {
    return this.http.get<Region[]>('/api/locations/regions');
  }

  cities(regionId: number): Observable<City[]> {
    return this.http.get<City[]>(`/api/locations/regions/${regionId}/cities`);
  }

  subCities(cityId: number): Observable<SubCity[]> {
    return this.http.get<SubCity[]>(`/api/locations/cities/${cityId}/sub-cities`);
  }
}