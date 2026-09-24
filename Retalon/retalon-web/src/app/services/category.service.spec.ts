import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting
} from '@angular/common/http/testing';

import { CategoryService } from './category.service';
import { environment } from '../../environments/environment';

describe('CategoryService', () => {
  let service: CategoryService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(CategoryService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('getCategories sends a GET to /api/categories', () => {
    service.getCategories().subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/api/categories`);
    expect(req.request.method).toBe('GET');

    req.flush([{ categoryId: 1, name: 'Dairy' }]);
  });
});
