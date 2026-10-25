import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AttachmentItem } from '../core/models/attachment.models';
import { RequestPhotos } from './request-photos';

const item = (n: number): AttachmentItem => ({
  id: `a${n}`,
  fileName: `photo-${n}.png`,
  contentType: 'image/png',
  sizeBytes: 2048,
  createdAt: '2026-10-25T08:00:00Z',
});

describe('RequestPhotos', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    httpMock = TestBed.inject(HttpTestingController);
  });

  async function render(editable: boolean, items: AttachmentItem[]): Promise<HTMLElement> {
    const fixture = TestBed.createComponent(RequestPhotos);
    fixture.componentRef.setInput('requestId', 'r1');
    fixture.componentRef.setInput('editable', editable);
    await fixture.whenStable();

    httpMock.expectOne('/api/requests/r1/attachments').flush(items);
    await fixture.whenStable();

    return fixture.nativeElement as HTMLElement;
  }

  const fileInput = (element: HTMLElement) => element.querySelector('input[type="file"]') as HTMLInputElement | null;

  it('shows the photos and an upload control to the owner', async () => {
    const element = await render(true, [item(1), item(2)]);

    expect(element.querySelectorAll('app-attachment-image')).toHaveLength(2);
    expect(element.textContent).toContain('photo-1.png');
    expect(element.textContent).toContain('2 of 5 photos');
    expect(fileInput(element)?.disabled).toBe(false);
    expect(fileInput(element)?.getAttribute('accept')).toBe('image/jpeg,image/png,image/webp');
  });

  it('shows the photos without any controls to a provider', async () => {
    const element = await render(false, [item(1), item(2)]);

    expect(element.querySelectorAll('app-attachment-image')).toHaveLength(2);
    expect(fileInput(element)).toBeNull();
    expect(element.textContent).not.toContain('Remove');
  });

  it('turns the upload control off when the request is full', async () => {
    const element = await render(true, [item(1), item(2), item(3), item(4), item(5)]);

    expect(element.textContent).toContain('5 of 5 photos');
    expect(fileInput(element)?.disabled).toBe(true);
  });

  it('explains an empty gallery differently to the owner and to a provider', async () => {
    const owner = await render(true, []);
    expect(owner.textContent).toContain('No photos yet');
  });
});