import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting
} from '@angular/common/http/testing';

import { CartService } from './cart.service';
import { environment } from '../../environments/environment';

describe('CartService', () => {
  let service: CartService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(CartService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('getCart sends a GET to /api/cart', () => {
    service.getCart().subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/api/cart`);
    expect(req.request.method).toBe('GET');

    req.flush({ cartId: 'c1', items: [], total: 0 });
  });

  it('addItem posts the product id and quantity', () => {
    service.addItem(7, 3).subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/api/cart/items`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ productId: 7, quantity: 3 });

    req.flush({ cartId: 'c1', items: [], total: 0 });
  });

  it('removeItem sends a DELETE with the cart item id in the path', () => {
    service.removeItem(99).subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/api/cart/items/99`);
    expect(req.request.method).toBe('DELETE');

    req.flush(null);
  });
});
