import { Component, DestroyRef, computed, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import { catchError, of, switchMap } from 'rxjs';
import { AttachmentApi } from '../core/api/attachment.api';

/**
 * Shows one private photo. The image is fetched with the sign-in token and shown from memory, so the token never
 * appears in a URL. The in-memory copy is released when the photo changes or the component goes away.
 */
@Component({
  selector: 'app-attachment-image',
  template: `
    @if (src(); as source) {
      <img [src]="source" [alt]="alt()" loading="lazy" />
    } @else if (failed()) {
      <div class="photo-missing" role="img" [attr.aria-label]="alt() + ' could not be loaded'">Photo unavailable</div>
    } @else {
      <div class="photo-loading" aria-hidden="true"></div>
    }
  `,
})
export class AttachmentImage {
  private readonly api = inject(AttachmentApi);
  private readonly sanitizer = inject(DomSanitizer);
  private objectUrl: string | null = null;

  readonly requestId = input.required<string>();
  readonly attachmentId = input.required<string>();
  readonly alt = input('Photo');

  protected readonly src = signal<SafeUrl | null>(null);
  protected readonly failed = signal(false);

  constructor() {
    const ids = computed(() => ({ requestId: this.requestId(), attachmentId: this.attachmentId() }));

    toObservable(ids)
      .pipe(
        switchMap(({ requestId, attachmentId }) => {
          this.failed.set(false);

          return this.api.image(requestId, attachmentId).pipe(
            catchError(() => {
              this.failed.set(true);

              return of(null);
            }),
          );
        }),
        takeUntilDestroyed(),
      )
      .subscribe((blob) => this.show(blob));

    inject(DestroyRef).onDestroy(() => this.release());
  }

  private show(blob: Blob | null): void {
    this.release();

    if (blob) {
      this.objectUrl = URL.createObjectURL(blob);
      // The URL points at a blob this app just fetched from its own API, so it is safe to trust.
      this.src.set(this.sanitizer.bypassSecurityTrustUrl(this.objectUrl));
    } else {
      this.src.set(null);
    }
  }

  private release(): void {
    if (this.objectUrl) {
      URL.revokeObjectURL(this.objectUrl);
      this.objectUrl = null;
    }
  }
}