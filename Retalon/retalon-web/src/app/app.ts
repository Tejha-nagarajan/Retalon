import { Component, signal } from '@angular/core';
import { Health } from './services/health';

@Component({
  selector: 'app-root',
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  healthStatus = signal('Checking API...');

  constructor(private health: Health) {
  console.log('Calling health API...');

  this.health.checkHealth().subscribe({
    next: (response) => {
      console.log('Health API response:', response);
      this.healthStatus.set('API is healthy');
    },
    error: (error) => {
      console.error('Health API error:', error);
      this.healthStatus.set('API is unavailable');
    },
    complete: () => {
      console.log('Health API request completed');
    }
  });
}
}