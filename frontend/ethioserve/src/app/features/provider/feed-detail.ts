import { DatePipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { distinctUntilChanged, map, switchMap } from 'rxjs';
import { LOADING, toLoadState } from '../../core/api/load-state';
import { RequestApi } from '../../core/api/request.api';
import { budgetText, statusBadge, statusLabel, whenText } from '../customer/request-status';
import { RequestPhotos } from '../../shared/request-photos';
import { ProviderNav } from './provider-nav';

@Component({
  selector: 'app-feed-detail',
  imports: [DatePipe, RouterLink, ProviderNav, RequestPhotos],
  template: `
    <div class="stack">
      <app-provider-nav />

      @let s = state();
      @if (s.kind === 'loading') {
        <p class="muted" role="status">Loading the request...</p>
      } @else if (s.kind === 'error') {
        <section class="card narrow">
          <h1>Request not available</h1>
          <p class="alert alert-error" role="alert">
            {{ s.httpStatus === 404 ? 'This request is no longer open, or is not one of yours to see.' : s.message }}
          </p>
          <a routerLink="/provider/requests" class="btn btn-primary">Back to requests</a>
        </section>
      } @else {
        <p><a routerLink="/provider/requests">&larr; All requests</a></p>

        <header class="card">
          <h1>{{ s.data.title }}</h1>
          <p><span [class]="badge(s.data.status)">{{ label(s.data.status) }}</span></p>
        </header>

        <section class="card" aria-labelledby="job-heading">
          <h2 id="job-heading">The job</h2>
          <dl class="details">
            <dt>Service</dt>
            <dd>{{ s.data.serviceName }} ({{ s.data.categoryName }})</dd>
            <dt>Where</dt>
            <dd>{{ s.data.cityName }}, {{ s.data.regionName }}</dd>
            <dt>Budget</dt>
            <dd>{{ budget(s.data.budgetMin, s.data.budgetMax) }}</dd>
            <dt>Preferred time</dt>
            <dd>{{ when(s.data.preferredDate, s.data.preferredTime) }}</dd>
            <dt>Posted</dt>
            <dd>{{ s.data.createdAt | date: 'medium' }}</dd>
          </dl>
          <h3>Description</h3>
          <p>{{ s.data.description }}</p>
        </section>

        <section class="card" aria-labelledby="photos-heading">
          <h2 id="photos-heading">Photos</h2>
          <app-request-photos [requestId]="s.data.id" [editable]="false" />
        </section>

        <section class="card" aria-labelledby="quote-heading">
          <h2 id="quote-heading">Respond to this request</h2>
          <p class="muted">
            Sending a quote opens in the next release. The customer's address and contact details are shared only after
            they accept your quote.
          </p>
        </section>
      }
    </div>
  `,
})
export class FeedDetailPage {
  private readonly api = inject(RequestApi);
  private readonly route = inject(ActivatedRoute);

  protected readonly label = statusLabel;
  protected readonly badge = statusBadge;
  protected readonly budget = budgetText;
  protected readonly when = whenText;

  protected readonly state = toSignal(
    this.route.paramMap.pipe(
      map((params) => params.get('id') ?? ''),
      distinctUntilChanged(),
      switchMap((id) => toLoadState(this.api.feedDetail(id))),
    ),
    { initialValue: LOADING },
  );
}