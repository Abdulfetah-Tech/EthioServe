export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface Category {
  id: string;
  name: string;
  description: string | null;
  iconKey: string | null;
  serviceCount: number;
}

export interface ServiceSummary {
  id: string;
  categoryId: string;
  categoryName: string;
  name: string;
  description: string | null;
  basePrice: number | null;
  pricingType: string;
}

export interface ProviderSummary {
  id: string;
  businessName: string;
  summary: string;
  yearsOfExperience: number;
  averageRating: number;
  totalReviews: number;
  cityName: string;
  subCityName: string | null;
  isAvailable: boolean;
  startingPrice: number;
}

export interface PublicOffer {
  serviceId: string;
  serviceName: string;
  categoryId: string;
  categoryName: string;
  price: number;
  description: string | null;
}

export interface PublicProvider {
  id: string;
  businessName: string;
  description: string;
  yearsOfExperience: number;
  averageRating: number;
  totalReviews: number;
  regionName: string;
  cityName: string;
  subCityName: string | null;
  isAvailable: boolean;
  services: PublicOffer[];
}

export interface Region {
  id: number;
  name: string;
  nameAm: string | null;
}

export interface City {
  id: number;
  regionId: number;
  name: string;
  nameAm: string | null;
}

export interface SubCity {
  id: number;
  cityId: number;
  name: string;
  nameAm: string | null;
}

export type ProviderSort = 'Rating' | 'Experience' | 'Price' | 'Name';

export interface ProviderSearchParams {
  search?: string;
  categoryId?: string;
  serviceId?: string;
  regionId?: number;
  cityId?: number;
  minRating?: number;
  availableOnly?: boolean;
  sort?: ProviderSort;
  page?: number;
}