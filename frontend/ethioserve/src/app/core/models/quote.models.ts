import { RequestStatus } from './request.models';

export type QuoteStatus = 'Pending' | 'Accepted' | 'Rejected' | 'Expired' | 'Cancelled';

/** The customer's details. The API sends them to the chosen provider only after their quote is accepted. */
export interface CustomerContact {
  name: string;
  phoneNumber: string;
  address: string;
}

/** A provider's own quote. */
export interface ProviderQuote {
  id: string;
  requestId: string;
  requestTitle: string;
  serviceName: string;
  cityName: string;
  requestStatus: RequestStatus;
  amount: number;
  description: string;
  estimatedDurationMinutes: number;
  validUntil: string;
  status: QuoteStatus;
  createdAt: string;
  contact: CustomerContact | null;
}

/** A quote as the customer sees it. The provider's phone number is present only on the accepted quote. */
export interface CustomerQuote {
  id: string;
  providerId: string;
  providerName: string;
  providerRating: number;
  providerReviews: number;
  providerYearsOfExperience: number;
  amount: number;
  description: string;
  estimatedDurationMinutes: number;
  validUntil: string;
  status: QuoteStatus;
  createdAt: string;
  providerPhone: string | null;
}

/** What a provider sends. The API decides the id, the status, the owner and the expiry date. */
export interface SubmitQuoteBody {
  amount: number;
  description: string;
  estimatedDurationMinutes: number;
  validForDays: number;
}