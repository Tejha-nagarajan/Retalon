import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';

import { Health } from '../../services/health';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './home.html'
})
export class Home implements OnInit {

  apiStatus = 'Checking API...';

  constructor(private health: Health, private cdr: ChangeDetectorRef) {}

  ngOnInit(): void {
    this.health.checkHealth().subscribe({
      next: () => {
        this.apiStatus = 'API is healthy';
        this.cdr.markForCheck();
      },
      error: () => {
        this.apiStatus = 'API unavailable';
        this.cdr.markForCheck();
      }
    });
  }
}
