import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AttachmentApi } from './attachment.api';

describe('AttachmentApi', () => {
  let api: AttachmentApi;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(AttachmentApi);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('lists the photos of a request', () => {
    api.list('r1').subscribe();

    const request = httpMock.expectOne('/api/requests/r1/attachments');
    expect(request.request.method).toBe('GET');
    request.flush([]);
  });

  it('uploads a photo as multipart form data under the name "file"', () => {
    const file = new File([new Uint8Array([1, 2, 3])], 'kitchen.png', { type: 'image/png' });

    api.upload('r1', file).subscribe();

    const request = httpMock.expectOne('/api/requests/r1/attachments');
    const body = request.request.body as FormData;

    expect(request.request.method).toBe('POST');
    expect(body).toBeInstanceOf(FormData);
    expect((body.get('file') as File).name).toBe('kitchen.png');
    request.flush({});
  });

  it('removes a photo with DELETE', () => {
    api.remove('r1', 'a1').subscribe();

    const request = httpMock.expectOne('/api/requests/r1/attachments/a1');
    expect(request.request.method).toBe('DELETE');
    request.flush(null);
  });

  it('fetches a photo as a blob', () => {
    let received: Blob | undefined;

    api.image('r1', 'a1').subscribe((blob) => (received = blob));

    const request = httpMock.expectOne('/api/requests/r1/attachments/a1');
    expect(request.request.responseType).toBe('blob');
    request.flush(new Blob(['x'], { type: 'image/png' }));
    expect(received?.type).toBe('image/png');
  });

  it('encodes ids in the URL', () => {
    api.image('a/b', 'c/d').subscribe();

    httpMock.expectOne('/api/requests/a%2Fb/attachments/c%2Fd').flush(new Blob());
  });
});