import { ChangeDetectorRef, Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { AuthService } from '../../services/auth.service';
import { Inventory as InventoryModel, InventoryService } from '../../services/inventory.service';
import { getErrorMessage } from '../../shared/error-message';

@Component({
  selector: 'app-inventory',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './inventory.html'
})
export class Inventory {

  productId: number | null = null;
  inventory: InventoryModel | null = null;

  editQuantityAvailable = 0;
  editQuantityReserved = 0;
  editSafetyStockLevel = 0;
  editProcurementLeadTimeDays = 0;

  restockQuantity = 0;

  message = '';
  isError = false;

  constructor(
    private auth: AuthService,
    private inventoryService: InventoryService,
    private cdr: ChangeDetectorRef
  ) {}

  isStaff(): boolean {
    return this.auth.isStaff();
  }

  lookup(): void {
    this.message = '';
    this.isError = false;
    this.inventory = null;

    if (!this.productId) {
      this.message = 'Enter a product ID first.';
      this.isError = true;
      return;
    }

    this.inventoryService.getByProductId(this.productId).subscribe({
      next: inventory => {
        this.inventory = inventory;

        this.editQuantityAvailable = inventory.quantityAvailable;
        this.editQuantityReserved = inventory.quantityReserved;
        this.editSafetyStockLevel = inventory.safetyStockLevel;
        this.editProcurementLeadTimeDays = inventory.procurementLeadTimeDays;
        this.cdr.markForCheck();
      },
      error: error => {
        this.isError = true;
        this.message = getErrorMessage(
          error,
          'Inventory not found for this product.'
        );
        this.cdr.markForCheck();
      }
    });
  }

  saveChanges(): void {
    if (!this.inventory) {
      return;
    }

    this.message = '';
    this.isError = false;

    this.inventoryService
      .update(this.inventory.productId, {
        quantityAvailable: this.editQuantityAvailable,
        quantityReserved: this.editQuantityReserved,
        safetyStockLevel: this.editSafetyStockLevel,
        procurementLeadTimeDays: this.editProcurementLeadTimeDays
      })
      .subscribe({
        next: inventory => {
          this.inventory = inventory;
          this.message = 'Inventory updated.';
          this.cdr.markForCheck();
        },
        error: error => {
          this.isError = true;
          this.message = getErrorMessage(error, 'Unable to update inventory.');
          this.cdr.markForCheck();
        }
      });
  }

  restock(): void {
    if (!this.inventory) {
      return;
    }

    this.message = '';
    this.isError = false;

    if (!this.restockQuantity || this.restockQuantity <= 0) {
      this.message = 'Enter a restock quantity greater than zero.';
      this.isError = true;
      return;
    }

    this.inventoryService
      .restock(this.inventory.productId, this.restockQuantity)
      .subscribe({
        next: inventory => {
          this.inventory = inventory;
          this.editQuantityAvailable = inventory.quantityAvailable;
          this.restockQuantity = 0;
          this.message = 'Inventory restocked.';
          this.cdr.markForCheck();
        },
        error: error => {
          this.isError = true;
          this.message = getErrorMessage(error, 'Unable to restock.');
          this.cdr.markForCheck();
        }
      });
  }
}
