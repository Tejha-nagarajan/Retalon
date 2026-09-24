import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { AuthService } from '../../services/auth.service';
import { Order, OrderService } from '../../services/order.service';
import {
  PROCUREMENT_STATUSES,
  Procurement as ProcurementModel,
  ProcurementService
} from '../../services/procurement.service';
import { badgeClass } from '../../shared/badge-class';
import { getErrorMessage } from '../../shared/error-message';

@Component({
  selector: 'app-procurement',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './procurement.html'
})
export class Procurement implements OnInit {

  badgeClass = badgeClass;

  statuses = PROCUREMENT_STATUSES;

  procurements: ProcurementModel[] = [];
  orders: Order[] = [];
  selectedProcurement: ProcurementModel | null = null;

  createOrderId: number | null = null;
  newStatus = '';

  message = '';
  isError = false;

  constructor(
    private auth: AuthService,
    private orderService: OrderService,
    private procurementService: ProcurementService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadProcurements();

    this.orderService.getOrders().subscribe({
      next: orders => {
        this.orders = orders;
        this.cdr.markForCheck();
      },
      error: () => {
        this.orders = [];
        this.cdr.markForCheck();
      }
    });
  }

  isStaff(): boolean {
    return this.auth.isStaff();
  }

  loadProcurements(): void {
    this.procurementService.getProcurements().subscribe({
      next: procurements => {
        this.procurements = procurements;
        this.cdr.markForCheck();
      },
      error: error => {
        this.isError = true;
        this.message = getErrorMessage(
          error,
          'Unable to load procurement records.'
        );
        this.cdr.markForCheck();
      }
    });
  }

  createProcurement(): void {
    this.message = '';
    this.isError = false;

    if (!this.createOrderId) {
      this.message = 'Select an order first.';
      this.isError = true;
      return;
    }

    this.procurementService.createProcurement(this.createOrderId).subscribe({
      next: created => {
        // The backend only creates a procurement record for items that
        // are short on inventory, so an empty result is a normal outcome.
        this.message =
          created.length > 0
            ? 'Procurement records created for this order.'
            : 'No procurement needed - all items in this order are in stock.';
        this.createOrderId = null;
        this.loadProcurements();
        this.cdr.markForCheck();
      },
      error: error => {
        this.isError = true;
        this.message = getErrorMessage(
          error,
          'Unable to create procurement records.'
        );
        this.cdr.markForCheck();
      }
    });
  }

  viewProcurement(procurement: ProcurementModel): void {
    this.selectedProcurement = procurement;
    this.newStatus = procurement.procurementStatus;
  }

  closeProcurement(): void {
    this.selectedProcurement = null;
  }

  updateStatus(): void {
    if (!this.selectedProcurement) {
      return;
    }

    const statusValue = this.statuses.indexOf(this.newStatus);

    if (statusValue < 0) {
      return;
    }

    this.procurementService
      .updateStatus(this.selectedProcurement.procurementId, statusValue)
      .subscribe({
        next: procurement => {
          this.selectedProcurement = procurement;
          this.message = 'Procurement status updated.';
          this.isError = false;
          this.loadProcurements();
          this.cdr.markForCheck();
        },
        error: error => {
          this.isError = true;
          this.message = getErrorMessage(
            error,
            'Unable to update procurement status.'
          );
          this.cdr.markForCheck();
        }
      });
  }
}
