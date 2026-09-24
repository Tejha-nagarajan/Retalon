import { ChangeDetectorRef, Component, EventEmitter, OnInit, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { AuthService } from '../../services/auth.service';
import { CartService } from '../../services/cart.service';
import { CategoryService, Category } from '../../services/category.service';
import { Product, ProductService } from '../../services/product.service';
import { badgeClass } from '../../shared/badge-class';
import { getErrorMessage } from '../../shared/error-message';

@Component({
  selector: 'app-products',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './products.html'
})
export class Products implements OnInit {

  @Output() goToLogin = new EventEmitter<void>();

  badgeClass = badgeClass;

  categories: Category[] = [];

  searchQuery = '';
  sortBy = '';
  descending = false;
  page = 1;
  pageSize = 10;
  totalPages = 0;

  products: Product[] = [];
  selectedProduct: Product | null = null;
  quantities: { [productId: number]: number } = {};

  searchMessage = '';
  cartMessage = '';

  barcodeInput = '';
  barcodeMessage = '';

  constructor(
    private auth: AuthService,
    private cart: CartService,
    private categoryService: CategoryService,
    private productService: ProductService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.categoryService.getCategories().subscribe({
      next: categories => {
        this.categories = categories;
        this.cdr.markForCheck();
      },
      error: () => {
        this.categories = [];
        this.cdr.markForCheck();
      }
    });
  }

  isLoggedIn(): boolean {
    return this.auth.isLoggedIn();
  }

  search(): void {
    this.searchMessage = '';
    this.page = 1;
    this.runSearch();
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages) {
      return;
    }

    this.page = page;
    this.runSearch();
  }

  private runSearch(): void {
    if (!this.searchQuery.trim()) {
      this.searchMessage = 'Enter a search term to find products.';
      this.products = [];
      this.totalPages = 0;
      return;
    }

    this.productService
      .search(
        this.searchQuery.trim(),
        this.page,
        this.pageSize,
        this.sortBy || undefined,
        this.descending
      )
      .subscribe({
        next: result => {
          this.products = result.items;
          this.totalPages = result.totalPages;

          this.searchMessage =
            this.products.length === 0 ? 'No products found.' : '';
          this.cdr.markForCheck();
        },
        error: error => {
          this.products = [];
          this.totalPages = 0;
          this.searchMessage = getErrorMessage(
            error,
            'Unable to search products.'
          );
          this.cdr.markForCheck();
        }
      });
  }

  viewProduct(product: Product): void {
    this.selectedProduct = product;
  }

  closeProduct(): void {
    this.selectedProduct = null;
  }

  addToCart(product: Product, quantity: number): void {
    this.cartMessage = '';

    if (!this.isLoggedIn()) {
      this.goToLogin.emit();
      return;
    }

    if (!quantity || quantity < 1) {
      quantity = 1;
    }

    this.cart.addItem(product.productId, quantity).subscribe({
      next: () => {
        this.cartMessage = `Added "${product.name}" to your cart.`;
        this.cdr.markForCheck();
      },
      error: error => {
        this.cartMessage = getErrorMessage(
          error,
          'Unable to add this product to the cart.'
        );
        this.cdr.markForCheck();
      }
    });
  }

  lookupBarcode(): void {
    this.barcodeMessage = '';

    if (!this.barcodeInput.trim()) {
      this.barcodeMessage = 'Enter a barcode first.';
      return;
    }

    this.productService.getByBarcode(this.barcodeInput.trim()).subscribe({
      next: product => {
        this.selectedProduct = product;
        this.barcodeMessage = '';
        this.cdr.markForCheck();
      },
      error: error => {
        this.barcodeMessage = getErrorMessage(
          error,
          'Product not found for this barcode.'
        );
        this.cdr.markForCheck();
      }
    });
  }

  importBarcode(): void {
    this.barcodeMessage = '';

    if (!this.barcodeInput.trim()) {
      this.barcodeMessage = 'Enter a barcode first.';
      return;
    }

    this.productService.importByBarcode(this.barcodeInput.trim()).subscribe({
      next: product => {
        this.selectedProduct = product;
        this.barcodeMessage = `Imported "${product.name}" from Open Food Facts.`;
        this.cdr.markForCheck();
      },
      error: error => {
        this.barcodeMessage = getErrorMessage(
          error,
          'Unable to import a product for this barcode.'
        );
        this.cdr.markForCheck();
      }
    });
  }
}
