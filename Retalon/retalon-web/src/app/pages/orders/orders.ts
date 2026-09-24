import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { Order, OrderService } from '../../services/order.service';
import { Payment, PaymentService } from '../../services/payment.service';
import { badgeClass } from '../../shared/badge-class';
import { getErrorMessage } from '../../shared/error-message';

// Stripe test mode never accepts a raw card number directly - it only
// confirms a PaymentIntent against a PaymentMethod id. These are Stripe's
// own fixed test PaymentMethod ids (https://stripe.com/docs/testing), keyed
// here by the well-known test card number that maps to each one, so the
// card form below can feel like a real checkout while still calling the
// existing confirm-test endpoint correctly.
const TEST_CARD_PAYMENT_METHODS: Record<string, string> = {
  '4242424242424242': 'pm_card_visa',
  '4000000000000002': 'pm_card_chargeDeclined',
  '4000000000009995': 'pm_card_chargeDeclinedInsufficientFunds',
  '4000000000000069': 'pm_card_chargeDeclinedExpiredCard',
  '4000000000000127': 'pm_card_chargeDeclinedIncorrectCvc'
};

@Component({
  selector: 'app-orders',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './orders.html'
})
export class Orders implements OnInit {

  badgeClass = badgeClass;

  orders: Order[] = [];
  selectedOrder: Order | null = null;

  currentPayment: Payment | null = null;

  cardName = '';
  cardNumber = '4242 4242 4242 4242';
  cardExpiry = '12/34';
  cardCvc = '123';
  cardError = '';

  message = '';
  isError = false;

  constructor(
    private orderService: OrderService,
    private paymentService: PaymentService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadOrders();
  }

  loadOrders(): void {
    this.message = '';

    this.orderService.getOrders().subscribe({
      next: orders => {
        this.orders = orders;
        this.cdr.markForCheck();
      },
      error: error => {
        this.isError = true;
        this.message = getErrorMessage(error, 'Unable to load your orders.');
        this.cdr.markForCheck();
      }
    });
  }

  viewOrder(order: Order): void {
    this.selectedOrder = order;
    this.currentPayment = null;
    this.message = '';
  }

  closeOrder(): void {
    this.selectedOrder = null;
    this.currentPayment = null;
    this.cardError = '';
  }

  onCardNumberInput(value: string): void {
    const digits = value.replace(/\D/g, '').slice(0, 19);
    this.cardNumber = digits.replace(/(.{4})/g, '$1 ').trim();
  }

  payNow(): void {
    if (!this.selectedOrder) {
      return;
    }

    this.message = '';
    this.isError = false;
    this.cardError = '';

    this.paymentService.createPayment(this.selectedOrder.orderId).subscribe({
      next: payment => {
        this.currentPayment = payment;
        this.cdr.markForCheck();
      },
      error: error => {
        this.isError = true;
        this.message = getErrorMessage(error, 'Unable to start payment.');
        this.cdr.markForCheck();
      }
    });
  }

  confirmPayment(): void {
    if (!this.currentPayment) {
      return;
    }

    this.message = '';
    this.isError = false;
    this.cardError = '';

    const digits = this.cardNumber.replace(/\D/g, '');
    const testPaymentMethod = TEST_CARD_PAYMENT_METHODS[digits];

    if (!testPaymentMethod) {
      this.cardError =
        'Unrecognized test card number. Try 4242 4242 4242 4242 (success) ' +
        'or 4000 0000 0000 0002 (declined).';
      return;
    }

    if (!/^\d{2}\/\d{2}$/.test(this.cardExpiry)) {
      this.cardError = 'Enter the expiry as MM/YY.';
      return;
    }

    if (!/^\d{3,4}$/.test(this.cardCvc)) {
      this.cardError = 'Enter a valid 3 or 4 digit CVC.';
      return;
    }

    this.paymentService
      .confirmTestPayment(this.currentPayment.paymentId, testPaymentMethod)
      .subscribe({
        next: payment => {
          this.currentPayment = payment;
          this.message =
            `Payment confirmed. Stripe payment ID: ${payment.stripePaymentIntentId}`;

          if (this.selectedOrder) {
            this.orderService
              .getOrder(this.selectedOrder.orderId)
              .subscribe(order => {
                this.selectedOrder = order;
                this.cdr.markForCheck();
              });
          }

          this.loadOrders();
          this.cdr.markForCheck();
        },
        error: error => {
          this.isError = true;
          this.message = getErrorMessage(error, 'Payment failed.');
          this.cdr.markForCheck();
        }
      });
  }
}
