import { Component, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { Subject, catchError, combineLatest, concatMap, from, map, of, startWith, switchMap } from 'rxjs';
import { AttachmentApi } from '../core/api/attachment.api';
import { toApiError } from '../core/api/api-error';
import { LOADING, LoadState, toLoadState } from '../core/api/load-state';
import { AttachmentItem } from '../core/models/attachment.models';
import { AttachmentImage } from './attachment-image';
import { ACCEPT_ATTRIBUTE, MAX_FILES, formatSize, validateFiles } from './attachment-rules';

function uploadError(error: unknown): string {
  const apiError = toApiError(error);

  return apiError.fieldErrors['file']?.[0] ?? apiError.message;
}

/** The photos of one request. Add and remove controls appear only when `editable` is true. */
@Component({
  selector: 'app-request-photos',
  imports: [AttachmentImage],
  template: `
    @let s = items();
    @if (s.kind === 'loading') {
      <p class="muted" role="status">Loading photos...</p>
    } @else if (s.kind === 'error') {
      <p class="alert alert-error" role="alert">{{ s.message }}</p>
    } @else {
      @if (s.data.length === 0) {
        <p class="muted">
          {{ editable() ? 'No photos yet. Add up to 5 photos so providers can see the problem.' : 'The customer did not add any photos.' }}
        </p>
      } @else {
        <ul class="photo-grid" role="list">
          @for (item of s.data; track item.id) {
            <li class="photo">
              <app-attachment-image [requestId]="requestId()" [attachmentId]="item.id" [alt]="item.fileName" />
              <p class="photo-caption">{{ item.fileName }} <span class="muted">({{ size(item.sizeBytes) }})</span></p>
              @if (editable()) {
                @if (confirming() === item.id) {
                  <div class="row-actions">
                    <span>Remove this photo?</span>
                    <button type="button" class="btn btn-danger" [disabled]="busy()" (click)="remove(item)">Yes, remove</button>
                    <button type="button" class="btn btn-secondary" (click)="confirming.set(null)">Keep</button>
                  </div>
                } @else {
                  <button type="button" class="btn btn-secondary" (click)="confirming.set(item.id)">Remove</button>
                }
              }
            </li>
          }
        </ul>
      }

      @if (editable()) {
        <div class="field">
          <label for="photo-input">Add photos</label>
          <input
            id="photo-input"
            type="file"
            multiple
            aria-describedby="photo-hint"
            [attr.accept]="accept"
            [disabled]="busy() || s.data.length >= max"
            (change)="onPick($event, s.data.length)"
          />
          <p class="hint" id="photo-hint">{{ s.data.length }} of {{ max }} photos. JPEG, PNG or WebP, up to 5 MB each.</p>
        </div>

        @if (progress(); as p) {
          <p role="status">Uploading {{ p.done }} of {{ p.total }}...</p>
        }

        @for (problem of problems(); track problem) {
          <p class="field-error" role="alert">{{ problem }}</p>
        }
      }
    }
  `,
})
export class RequestPhotos {
  private readonly api = inject(AttachmentApi);
  private readonly refresh$ = new Subject<void>();

  readonly requestId = input.required<string>();
  readonly editable = input(false);

  protected readonly accept = ACCEPT_ATTRIBUTE;
  protected readonly max = MAX_FILES;
  protected readonly size = formatSize;

  protected readonly items = signal<LoadState<AttachmentItem[]>>(LOADING);
  protected readonly confirming = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly progress = signal<{ done: number; total: number } | null>(null);
  protected readonly problems = signal<string[]>([]);

  constructor() {
    combineLatest([toObservable(this.requestId), this.refresh$.pipe(startWith(undefined))])
      .pipe(
        switchMap(([id]) => toLoadState(this.api.list(id))),
        takeUntilDestroyed(),
      )
      .subscribe((state) => this.items.set(state));
  }

  protected onPick(event: Event, currentCount: number): void {
    const element = event.target as HTMLInputElement;
    const { accepted, problems } = validateFiles(Array.from(element.files ?? []), currentCount);
    element.value = ''; // lets the same file be picked again

    this.problems.set(problems);

    if (accepted.length === 0) {
      return;
    }

    const requestId = this.requestId();
    const failures: string[] = [];
    let done = 0;

    this.busy.set(true);
    this.progress.set({ done, total: accepted.length });

    // One at a time, so a failure never hides the files that did upload.
    from(accepted)
      .pipe(
        concatMap((file) =>
          this.api.upload(requestId, file).pipe(
            map(() => null as string | null),
            catchError((error: unknown) => of(`${file.name}: ${uploadError(error)}`)),
          ),
        ),
      )
      .subscribe({
        next: (failure) => {
          done++;

          if (failure) {
            failures.push(failure);
          }

          this.progress.set({ done, total: accepted.length });
        },
        complete: () => {
          this.progress.set(null);
          this.busy.set(false);
          this.problems.set([...this.problems(), ...failures]);
          this.refresh$.next();
        },
      });
  }

  protected remove(item: AttachmentItem): void {
    this.busy.set(true);
    this.problems.set([]);

    this.api.remove(this.requestId(), item.id).subscribe({
      next: () => {
        this.confirming.set(null);
        this.busy.set(false);
        this.refresh$.next();
      },
      error: (error: unknown) => {
        this.problems.set([toApiError(error).message]);
        this.busy.set(false);
      },
    });
  }
}