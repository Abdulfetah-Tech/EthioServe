export type PricingType = 'Fixed' | 'Hourly' | 'Negotiable' | 'StartingFrom';

export interface AdminCategory {
  id: string;
  name: string;
  description: string | null;
  iconKey: string | null;
  isActive: boolean;
  serviceCount: number;
  activeServiceCount: number;
  updatedAt: string;
}

export interface AdminService {
  id: string;
  categoryId: string;
  categoryName: string;
  name: string;
  description: string | null;
  basePrice: number | null;
  pricingType: PricingType;
  isActive: boolean;
  updatedAt: string;
}

export interface CategoryRequest {
  name: string;
  description: string | null;
  iconKey: string | null;
}

export interface CreateServiceRequest {
  categoryId: string;
  name: string;
  description: string | null;
  basePrice: number | null;
  pricingType: PricingType;
}

export interface UpdateServiceRequest {
  name: string;
  description: string | null;
  basePrice: number | null;
  pricingType: PricingType;
}

export interface AdminServiceQuery {
  categoryId?: string;
  search?: string;
  includeInactive?: boolean;
  page?: number;
}