import { ChangeDetectorRef, Component, EventEmitter, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { AuthService } from '../../services/auth.service';
import { getErrorMessage } from '../../shared/error-message';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './login.html'
})
export class Login {

  @Output() loggedIn = new EventEmitter<void>();

  email = '';
  password = '';
  message = '';
  isSubmitting = false;

  constructor(private auth: AuthService, private cdr: ChangeDetectorRef) {}

  submit(): void {
    this.message = '';

    if (!this.email || !this.password) {
      this.message = 'Email and password are required.';
      return;
    }

    this.isSubmitting = true;

    this.auth
      .login({
        email: this.email,
        password: this.password
      })
      .subscribe({
        next: () => {
          this.isSubmitting = false;
          this.email = '';
          this.password = '';
          this.loggedIn.emit();
          this.cdr.markForCheck();
        },
        error: error => {
          this.isSubmitting = false;

          if (error.status === 401) {
            this.message = 'Invalid email or password.';
          } else {
            this.message = getErrorMessage(
              error,
              'Unable to login. Please try again.'
            );
          }

          this.cdr.markForCheck();
        }
      });
  }
}
