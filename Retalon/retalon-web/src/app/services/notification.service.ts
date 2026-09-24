import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class NotificationService {

  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  sendTestEmail(
    toEmail: string,
    subject: string,
    body: string
  ): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(
      `${this.apiUrl}/api/notifications/test-email`,
      { toEmail, subject, body }
    );
  }
}
