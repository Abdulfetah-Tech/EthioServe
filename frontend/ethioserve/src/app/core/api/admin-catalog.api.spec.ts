import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AdminCatalogApi } from './admin-catalog.api';

const EMPTY_PAGE = { items: [], page: 1, pageSize: 15, totalCount: 0, totalPages: 0 };

describe('AdminCatalogApi', () => {
  let api: AdminCatalogApi;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(AdminCatalogApi);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('lists and creates categories', () => {
    api.categories().subscribe();
    httpMock.expectOne('/api/admin/categories').flush([]);

    api.createCategory({ name: 'Plumbing', description: null, iconKey: null }).subscribe();
    const create = httpMock.expectOne('/api/admin/categories');
    expect(create.request.method).toBe('POST');
    expect(create.request.body).toEqual({ name: 'Plumbing', description: null, iconKey: null });
    create.flush({});
  });

  it('updates a category and switches it on and off', () => {
    api.updateCategory('c1', { name: 'New', description: 'x', iconKey: null }).subscribe();
    const update = httpMock.expectOne('/api/admin/categories/c1');
    expect(update.request.method).toBe('PUT');
    update.flush({});

    api.setCategoryActive('c1', false).subscribe();
    expect(httpMock.expectOne('/api/admin/categories/c1/deactivate').request.method).toBe('POST');

    api.setCategoryActive('c1', true).subscribe();
    expect(httpMock.expectOne('/api/admin/categories/c1/activate').request.method).toBe('POST');
  });

  it('sends only the service filters that differ from the defaults', () => {
    api.services({ categoryId: 'c1', search: 'pipe', includeInactive: false, page: 2 }).subscribe();

    const request = httpMock.expectOne((r) => r.url === '/api/admin/services');

    expect(request.request.params.get('categoryId')).toBe('c1');
    expect(request.request.params.get('search')).toBe('pipe');
    expect(request.request.params.get('includeInactive')).toBe('false');
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('15');
    request.flush(EMPTY_PAGE);
  });

  it('leaves includeInactive and page 1 out when they are the defaults', () => {
    api.services({ includeInactive: true, page: 1 }).subscribe();

    const request = httpMock.expectOne((r) => r.url === '/api/admin/services');

    expect(request.request.params.has('includeInactive')).toBe(false);
    expect(request.request.params.has('page')).toBe(false);
    request.flush(EMPTY_PAGE);
  });

  it('creates, updates and switches services', () => {
    api.createService({ categoryId: 'c1', name: 'Pipe repair', description: null, basePrice: 300, pricingType: 'Fixed' }).subscribe();
    const create = httpMock.expectOne('/api/admin/services');
    expect(create.request.method).toBe('POST');
    expect(create.request.body.basePrice).toBe(300);
    create.flush({});

    api.updateService('s1', { name: 'Pipe repair', description: null, basePrice: null, pricingType: 'Negotiable' }).subscribe();
    const update = httpMock.expectOne('/api/admin/services/s1');
    expect(update.request.method).toBe('PUT');
    expect(update.request.body.pricingType).toBe('Negotiable');
    update.flush({});

    api.setServiceActive('s1', false).subscribe();
    httpMock.expectOne('/api/admin/services/s1/deactivate').flush({});

    api.setServiceActive('s1', true).subscribe();
    httpMock.expectOne('/api/admin/services/s1/activate').flush({});
  });

  it('encodes ids in the URL', () => {
    api.setServiceActive('a/b', true).subscribe();

    httpMock.expectOne('/api/admin/services/a%2Fb/activate').flush({});
  });
});