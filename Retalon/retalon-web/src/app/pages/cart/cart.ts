import { ChangeDetectorRef, Component, EventEmitter, OnInit, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

import { Cart as CartModel, CartService } from '../../services/cart.service';
import { OrderService } from '../../services/order.service';
import { getErrorMessage } from '../../shared/error-message';

@Component({
  selector: 'app-cart',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './cart.html'
})
export class Cart implements OnInit {

  @Output() orderPlaced = new EventEmitter<void>();

  cart: CartModel | null = null;
  message = '';
  isError = false;
  isPlacingOrder = false;

  constructor(
    private cartService: CartService,
    private orderService: OrderService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadCart();
  }

  loadCart(): void {
    this.message = '';

    this.cartService.getCart().subscribe({
      next: cart => {
        this.cart = cart;
        this.cdr.markForCheck();
      },
      error: error => {
        this.isError = true;
        this.message = getErrorMessage(error, 'Unable to load your cart.');
        this.cdr.markForCheck();
      }
    });
  }

  removeItem(cartItemId: number): void {
    this.cartService.removeItem(cartItemId).subscribe({
      next: () => {
        this.loadCart();
      },
      error: error => {
        this.isError = true;
        this.message = getErrorMessage(error, 'Unable to remove this item.');
        this.cdr.markForCheck();
      }
    });
  }

  placeOrder(): void {
    this.message = '';
    this.isError = false;
    this.isPlacingOrder = true;

    this.orderService.createOrder().subscribe({
      next: () => {
        this.isPlacingOrder = false;
        this.orderPlaced.emit();
        this.cdr.markForCheck();
      },
      error: error => {
        this.isPlacingOrder = false;
        this.isError = true;
        this.message = getErrorMessage(error, 'Unable to place your order.');
        this.cdr.markForCheck();
      }
    });
  }
}
