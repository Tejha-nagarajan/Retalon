import { ChangeDetectorRef, Component, EventEmitter, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { AuthService } from '../../services/auth.service';
import { getErrorMessage } from '../../shared/error-message';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './register.html'
})
export class Register {

  @Output() registered = new EventEmitter<void>();

  firstName = '';
  lastName = '';
  email = '';
  password = '';
  phoneNumber = '';
  addressLine1 = '';
  addressLine2 = '';
  city = '';
  state = '';
  postalCode = '';
  country = '';

  message = '';
  isError = false;
  isSubmitting = false;

  constructor(private auth: AuthService, private cdr: ChangeDetectorRef) {}

  submit(): void {
    this.message = '';
    this.isError = false;

    if (
      !this.firstName ||
      !this.lastName ||
      !this.email ||
      !this.password ||
      !this.addressLine1 ||
      !this.city ||
      !this.postalCode ||
      !this.country
    ) {
      this.message = 'Please fill in all required fields.';
      this.isError = true;
      return;
    }

    this.isSubmitting = true;

    this.auth
      .register({
        firstName: this.firstName,
        lastName: this.lastName,
        email: this.email,
        password: this.password,
        phoneNumber: this.phoneNumber,
        addressLine1: this.addressLine1,
        addressLine2: this.addressLine2,
        city: this.city,
        state: this.state,
        postalCode: this.postalCode,
        country: this.country
      })
      .subscribe({
        next: () => {
          this.isSubmitting = false;
          this.message = 'Registration successful. You can now log in.';
          this.isError = false;
          this.registered.emit();
          this.cdr.markForCheck();
        },
        error: error => {
          this.isSubmitting = false;
          this.isError = true;
          this.message = getErrorMessage(
            error,
            'Unable to register. Please try again.'
          );
          this.cdr.markForCheck();
        }
      });
  }
}
