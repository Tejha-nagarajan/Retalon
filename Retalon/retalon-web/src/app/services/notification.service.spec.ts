import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting
} from '@angular/common/http/testing';

import { NotificationService } from './notification.service';
import { environment } from '../../environments/environment';

describe('NotificationService', () => {
  let service: NotificationService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(NotificationService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('sendTestEmail posts toEmail, subject and body', () => {
    service.sendTestEmail('a@b.com', 'Hi', 'Body text').subscribe();

    const req = httpMock.expectOne(
      `${environment.apiUrl}/api/notifications/test-email`
    );

    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      toEmail: 'a@b.com',
      subject: 'Hi',
      body: 'Body text'
    });

    req.flush({ message: 'Test email sent successfully.' });
  });
});
