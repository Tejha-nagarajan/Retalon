import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting
} from '@angular/common/http/testing';

import { App } from './app';
import { AuthService } from './services/auth.service';
import { environment } from '../environments/environment';

class FakeAuthService {
  loggedIn = false;
  staff = false;

  isLoggedIn(): boolean {
    return this.loggedIn;
  }

  isStaff(): boolean {
    return this.staff;
  }

  logout(): void {
    this.loggedIn = false;
  }
}

describe('App', () => {
  let fakeAuth: FakeAuthService;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    fakeAuth = new FakeAuthService();

    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: fakeAuth }
      ]
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    // The Home page (shown by default) checks API health on init.
    httpMock.expectOne(`${environment.apiUrl}/health`).flush({ status: 'Healthy' });
    httpMock.verify();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('renders the Retalon logo and the welcome page by default', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.logo')?.textContent).toContain('Retalon');
    expect(compiled.querySelector('h1')?.textContent).toContain('Welcome to Retalon');
  });

  it('hides account-only nav links when logged out, and shows Login/Register', () => {
    fakeAuth.loggedIn = false;

    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Login');
    expect(text).toContain('Register');
    expect(text).not.toContain('Logout');
  });

  it('shows account-only nav links when logged in, and hides Login/Register', () => {
    fakeAuth.loggedIn = true;

    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Cart');
    expect(text).toContain('Orders');
    expect(text).toContain('Logout');
    expect(text).not.toContain('Register');
  });

  it('hides Admin Import from a logged-in non-staff user', () => {
    fakeAuth.loggedIn = true;
    fakeAuth.staff = false;

    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).not.toContain('Admin Import');
  });

  it('shows Admin Import to a logged-in staff user', () => {
    fakeAuth.loggedIn = true;
    fakeAuth.staff = true;

    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Admin Import');
  });

  it('logout() clears the session and returns to the home page', () => {
    fakeAuth.loggedIn = true;

    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    // 'login' is used here (rather than e.g. 'cart') because it renders
    // without making any HTTP calls of its own.
    fixture.componentInstance.goTo('login');
    fixture.componentInstance.logout();

    expect(fixture.componentInstance.currentPage).toBe('home');
    expect(fakeAuth.loggedIn).toBe(false);
  });
});
