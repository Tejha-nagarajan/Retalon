import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting
} from '@angular/common/http/testing';

import { Products } from './products';
import { AuthService } from '../../services/auth.service';
import { environment } from '../../../environments/environment';

class FakeAuthService {
  loggedIn = false;
  isLoggedIn(): boolean {
    return this.loggedIn;
  }
}

describe('Products', () => {
  let component: Products;
  let httpMock: HttpTestingController;
  let fakeAuth: FakeAuthService;

  beforeEach(() => {
    fakeAuth = new FakeAuthService();

    TestBed.configureTestingModule({
      imports: [Products],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: fakeAuth }
      ]
    });

    const fixture = TestBed.createComponent(Products);
    component = fixture.componentInstance;
    fixture.detectChanges();

    httpMock = TestBed.inject(HttpTestingController);

    // ngOnInit loads categories; keep tests focused by draining it here.
    httpMock.expectOne(`${environment.apiUrl}/api/categories`).flush([]);
  });

  afterEach(() => httpMock.verify());

  it('does not search when the query is empty', () => {
    component.searchQuery = '   ';
    component.search();

    expect(component.searchMessage).toBe(
      'Enter a search term to find products.'
    );
    httpMock.expectNone(r => r.url.includes('/api/products/search'));
  });

  it('searches with the trimmed query and shows a message when nothing is found', () => {
    component.searchQuery = '  milk  ';
    component.search();

    const req = httpMock.expectOne(
      r => r.url === `${environment.apiUrl}/api/products/search`
    );
    expect(req.request.params.get('query')).toBe('milk');

    req.flush({ items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0 });

    expect(component.products).toEqual([]);
    expect(component.searchMessage).toBe('No products found.');
  });

  it('redirects to login instead of calling the cart API when logged out', () => {
    fakeAuth.loggedIn = false;

    let redirected = false;
    component.goToLogin.subscribe(() => (redirected = true));

    component.addToCart(
      { productId: 1, name: 'Milk' } as any,
      1
    );

    expect(redirected).toBe(true);
    httpMock.expectNone(`${environment.apiUrl}/api/cart/items`);
  });

  it('adds to the cart when logged in, defaulting quantity to 1 if unset', () => {
    fakeAuth.loggedIn = true;

    component.addToCart(
      { productId: 1, name: 'Milk' } as any,
      undefined as unknown as number
    );

    const req = httpMock.expectOne(`${environment.apiUrl}/api/cart/items`);
    expect(req.request.body).toEqual({ productId: 1, quantity: 1 });

    req.flush({ cartId: 'c1', items: [], total: 0 });

    expect(component.cartMessage).toContain('Milk');
  });
});
