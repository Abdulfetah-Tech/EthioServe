import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MarketplaceApi } from './marketplace.api';

const EMPTY_PAGE = { items: [], page: 1, pageSize: 12, totalCount: 0, totalPages: 0 };

describe('MarketplaceApi', () => {
  let api: MarketplaceApi;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(MarketplaceApi);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('loads categories', () => {
    api.categories().subscribe();

    const request = httpMock.expectOne('/api/categories');
    expect(request.request.method).toBe('GET');
    request.flush([]);
  });

  it('sends only the filters that are set when searching providers', () => {
    api
      .searchProviders({ categoryId: 'cat-1', minRating: 4, availableOnly: true, sort: 'Price', page: 2 }, 6)
      .subscribe();

    const request = httpMock.expectOne((r) => r.url === '/api/providers');
    const params = request.request.params;

    expect(params.get('categoryId')).toBe('cat-1');
    expect(params.get('minRating')).toBe('4');
    expect(params.get('availableOnly')).toBe('true');
    expect(params.get('sort')).toBe('Price');
    expect(params.get('page')).toBe('2');
    expect(params.get('pageSize')).toBe('6');
    expect(params.has('search')).toBe(false);
    expect(params.has('serviceId')).toBe(false);
    expect(params.has('cityId')).toBe(false);
    request.flush(EMPTY_PAGE);
  });

  it('does not send availableOnly when it is false and defaults the page size to 12', () => {
    api.searchProviders({ availableOnly: false }).subscribe();

    const request = httpMock.expectOne((r) => r.url === '/api/providers');

    expect(request.request.params.has('availableOnly')).toBe(false);
    expect(request.request.params.get('pageSize')).toBe('12');
    request.flush(EMPTY_PAGE);
  });

  it('encodes the provider id in the URL', () => {
    api.provider('a/b').subscribe();

    httpMock.expectOne('/api/providers/a%2Fb').flush({});
  });

  it('loads the services of a category with the requested page size', () => {
    api.services({ categoryId: 'cat-1', pageSize: 50 }).subscribe();

    const request = httpMock.expectOne((r) => r.url === '/api/services');

    expect(request.request.params.get('categoryId')).toBe('cat-1');
    expect(request.request.params.get('pageSize')).toBe('50');
    request.flush(EMPTY_PAGE);
  });

  it('loads regions, cities and sub-cities from the location endpoints', () => {
    api.regions().subscribe();
    httpMock.expectOne('/api/locations/regions').flush([]);

    api.cities(3).subscribe();
    httpMock.expectOne('/api/locations/regions/3/cities').flush([]);

    api.subCities(7).subscribe();
    httpMock.expectOne('/api/locations/cities/7/sub-cities').flush([]);
  });
});