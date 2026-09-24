import { ChangeDetectorRef, Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { NotificationService } from '../../services/notification.service';
import { getErrorMessage } from '../../shared/error-message';

@Component({
  selector: 'app-notifications',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './notifications.html'
})
export class Notifications {

  toEmail = '';
  subject = 'Retalon Test Email';
  body = 'This is a test email from Retalon.';

  message = '';
  isError = false;
  isSubmitting = false;

  constructor(
    private notificationService: NotificationService,
    private cdr: ChangeDetectorRef
  ) {}

  send(): void {
    this.message = '';
    this.isError = false;

    if (!this.toEmail) {
      this.message = 'Enter a recipient email address.';
      this.isError = true;
      return;
    }

    this.isSubmitting = true;

    this.notificationService
      .sendTestEmail(this.toEmail, this.subject, this.body)
      .subscribe({
        next: response => {
          this.isSubmitting = false;
          this.message = response.message;
          this.cdr.markForCheck();
        },
        error: error => {
          this.isSubmitting = false;
          this.isError = true;
          this.message = getErrorMessage(
            error,
            'Unable to send the test email.'
          );
          this.cdr.markForCheck();
        }
      });
  }
}
