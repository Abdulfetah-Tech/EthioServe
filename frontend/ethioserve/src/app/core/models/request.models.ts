export type RequestStatus =
  | 'Pending'
  | 'Quoted'
  | 'Accepted'
  | 'Scheduled'
  | 'InProgress'
  | 'Completed'
  | 'Cancelled'
  | 'Rejected';

/** The customer's own view of a request, including the exact address. */
export interface RequestDetail {
  id: string;
  serviceId: string;
  serviceName: string;
  categoryId: string;
  categoryName: string;
  title: string;
  description: string;
  address: string;
  regionId: number;
  regionName: string;
  cityId: number;
  cityName: string;
  budgetMin: number | null;
  budgetMax: number | null;
  preferredDate: string | null;
  preferredTime: string | null;
  status: RequestStatus;
  cancelledReason: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface RequestListItem {
  id: string;
  title: string;
  serviceId: string;
  serviceName: string;
  categoryName: string;
  cityName: string;
  budgetMin: number | null;
  budgetMax: number | null;
  preferredDate: string | null;
  status: RequestStatus;
  createdAt: string;
}

/** What a customer may set. There is no id, owner, status or reason: the API would ignore them anyway. */
export interface JobRequestBody {
  title: string;
  description: string;
  address: string;
  regionId: number;
  cityId: number;
  budgetMin: number | null;
  budgetMax: number | null;
  preferredDate: string | null;
  preferredTime: string | null;
}

export interface CreateRequestBody extends JobRequestBody {
  serviceId: string;
}

/** What a provider sees in the feed: the job, never who asked and never the address. */
export interface FeedRequestItem {
  id: string;
  title: string;
  summary: string;
  serviceId: string;
  serviceName: string;
  categoryName: string;
  cityName: string;
  budgetMin: number | null;
  budgetMax: number | null;
  preferredDate: string | null;
  preferredTime: string | null;
  status: RequestStatus;
  createdAt: string;
}

export interface FeedRequestDetail {
  id: string;
  title: string;
  description: string;
  serviceId: string;
  serviceName: string;
  categoryId: string;
  categoryName: string;
  regionName: string;
  cityName: string;
  budgetMin: number | null;
  budgetMax: number | null;
  preferredDate: string | null;
  preferredTime: string | null;
  status: RequestStatus;
  createdAt: string;
}