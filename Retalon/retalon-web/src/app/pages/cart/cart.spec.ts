import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting
} from '@angular/common/http/testing';

import { Cart } from './cart';
import { environment } from '../../../environments/environment';

describe('Cart', () => {
  let component: Cart;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [Cart],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });

    const fixture = TestBed.createComponent(Cart);
    component = fixture.componentInstance;
    fixture.detectChanges();

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  function flushInitialCart(items: unknown[] = []): void {
    httpMock
      .expectOne(`${environment.apiUrl}/api/cart`)
      .flush({ cartId: 'c1', items, total: 0 });
  }

  it('loads the cart on init', () => {
    flushInitialCart([
      { cartItemId: 1, productId: 1, productName: 'Milk', price: 2, quantity: 3, subtotal: 6 }
    ]);

    expect(component.cart?.items.length).toBe(1);
  });

  it('removeItem deletes the item then reloads the cart', () => {
    flushInitialCart();

    component.removeItem(1);

    const deleteReq = httpMock.expectOne(`${environment.apiUrl}/api/cart/items/1`);
    expect(deleteReq.request.method).toBe('DELETE');
    deleteReq.flush(null);

    // loadCart() runs again after a successful removal.
    httpMock.expectOne(`${environment.apiUrl}/api/cart`).flush({
      cartId: 'c1',
      items: [],
      total: 0
    });

    expect(component.cart?.items.length).toBe(0);
  });

  it('placeOrder emits orderPlaced on success', () => {
    flushInitialCart();

    let emitted = false;
    component.orderPlaced.subscribe(() => (emitted = true));

    component.placeOrder();

    const req = httpMock.expectOne(`${environment.apiUrl}/api/orders`);
    expect(req.request.method).toBe('POST');
    req.flush({
      orderId: 1,
      orderStatus: 'Pending',
      totalAmount: 6,
      expectedDeliveryDate: '2026-01-01'
    });

    expect(emitted).toBe(true);
  });

  it('placeOrder shows an error message and does not emit on failure', () => {
    flushInitialCart();

    let emitted = false;
    component.orderPlaced.subscribe(() => (emitted = true));

    component.placeOrder();

    const req = httpMock.expectOne(`${environment.apiUrl}/api/orders`);
    req.flush({ message: 'Cart is empty.' }, { status: 400, statusText: 'Bad Request' });

    expect(emitted).toBe(false);
    expect(component.isError).toBe(true);
    expect(component.message).toBe('Cart is empty.');
  });
});
