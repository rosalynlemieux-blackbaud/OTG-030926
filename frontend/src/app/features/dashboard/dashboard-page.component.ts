import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';

interface SparkIdeaResponse {
  conversationId: string;
  reply: string;
  readyToSubmit: boolean;
  ideaId?: string;
}

@Component({
  selector: 'otg-dashboard-page',
  standalone: true,
  template: `
    <h2>Dashboard</h2>
    <p>Role-specific SKY UX dashboard content will be implemented in subsequent steps.</p>
    <section class="spark-panel">
      <h3>Spark an Idea</h3>
      <textarea #messageInput rows="3" placeholder="Describe a challenge or idea direction"></textarea>
      <div>
        <button type="button" (click)="sendSpark(messageInput.value)">Send</button>
      </div>
      @if (reply()) {
        <p>{{ reply() }}</p>
      }
    </section>
  `,
  styles: [
    `
      .spark-panel { margin-top: 1rem; border: 1px solid #ddd; padding: 1rem; border-radius: 8px; display: grid; gap: 0.5rem; }
      textarea { width: 100%; }
      button { width: fit-content; padding: 0.5rem 1rem; }
    `
  ]
})
export class DashboardPageComponent {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly conversationId = signal<string | null>(null);
  readonly reply = signal<string>('');

  sendSpark(message: string): void {
    const trimmed = message.trim();
    if (!trimmed) {
      return;
    }

    this.http
      .post<SparkIdeaResponse>('/api/ideas/spark', {
        conversationId: this.conversationId(),
        hackathonId: 'default',
        message: trimmed
      })
      .subscribe({
        next: (response) => {
          this.conversationId.set(response.conversationId);
          this.reply.set(response.reply);
          if (response.readyToSubmit && response.ideaId) {
            this.router.navigateByUrl(`/ideas/${response.ideaId}`);
          }
        },
        error: () => {
          this.reply.set('Unable to generate an idea right now.');
        }
      });
  }
}
