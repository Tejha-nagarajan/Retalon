import { ChangeDetectorRef, Component } from '@angular/core';
import { CommonModule } from '@angular/common';

import { AuthService } from './services/auth.service';

import { Home } from './pages/home/home';
import { Register } from './pages/register/register';
import { Login } from './pages/login/login';
import { Products } from './pages/products/products';
import { Cart } from './pages/cart/cart';
import { Orders } from './pages/orders/orders';
import { Procurement } from './pages/procurement/procurement';
import { Inventory } from './pages/inventory/inventory';
import { Notifications } from './pages/notifications/notifications';
import { AdminImport } from './pages/admin-import/admin-import';

type Page =
  | 'home'
  | 'register'
  | 'login'
  | 'products'
  | 'cart'
  | 'orders'
  | 'procurement'
  | 'inventory'
  | 'notifications'
  | 'admin-import';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    CommonModule,
    Home,
    Register,
    Login,
    Products,
    Cart,
    Orders,
    Procurement,
    Inventory,
    Notifications,
    AdminImport
  ],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {

  currentPage: Page = 'home';

  constructor(private auth: AuthService, private cdr: ChangeDetectorRef) {}

  goTo(page: Page): void {
    this.currentPage = page;
    this.cdr.markForCheck();
  }

  isLoggedIn(): boolean {
    return this.auth.isLoggedIn();
  }

  isStaff(): boolean {
    return this.auth.isStaff();
  }

  logout(): void {
    this.auth.logout();
    this.currentPage = 'home';
    this.cdr.markForCheck();
  }
}
