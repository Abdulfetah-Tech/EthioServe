import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AttachmentItem } from '../models/attachment.models';

/** Photos on a request. Only the owner can add or remove; a provider who can see the request can look. */
@Injectable({ providedIn: 'root' })
export class AttachmentApi {
  private readonly http = inject(HttpClient);

  list(requestId: string): Observable<AttachmentItem[]> {
    return this.http.get<AttachmentItem[]>(this.base(requestId));
  }

  upload(requestId: string, file: File): Observable<AttachmentItem> {
    const form = new FormData();
    form.append('file', file, file.name);

    return this.http.post<AttachmentItem>(this.base(requestId), form);
  }

  remove(requestId: string, id: string): Observable<void> {
    return this.http.delete<void>(this.one(requestId, id));
  }

  /** The photo as a blob. An image tag cannot send the sign-in token, so the app fetches it itself. */
  image(requestId: string, id: string): Observable<Blob> {
    return this.http.get(this.one(requestId, id), { responseType: 'blob' });
  }

  private base(requestId: string): string {
    return `/api/requests/${encodeURIComponent(requestId)}/attachments`;
  }

  private one(requestId: string, id: string): string {
    return `${this.base(requestId)}/${encodeURIComponent(id)}`;
  }
}