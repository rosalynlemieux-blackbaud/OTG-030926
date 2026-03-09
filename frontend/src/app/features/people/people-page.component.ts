import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';

interface PersonItem {
  id: string;
  name?: string;
  email: string;
  department?: string;
  location?: string;
  topSkills: string[];
}

@Component({
  selector: 'otg-people-page',
  standalone: true,
  template: `
    <h2>People</h2>
    <div class="search-row">
      <input #queryInput type="text" placeholder="Search by name, email, department" />
      <button type="button" (click)="search(queryInput.value)">Search</button>
    </div>

    @if (loading()) {
      <p>Loading...</p>
    } @else if (!people().length) {
      <p>No people found.</p>
    } @else {
      <div class="card-grid">
        @for (person of people(); track person.id) {
          <article class="card">
            <h3>{{ person.name || person.email }}</h3>
            <p>{{ person.email }}</p>
            @if (person.department) { <p>Department: {{ person.department }}</p> }
            @if (person.location) { <p>Location: {{ person.location }}</p> }
            @if (person.topSkills.length) {
              <p>Top skills: {{ person.topSkills.join(', ') }}</p>
            }
          </article>
        }
      </div>
    }
  `,
  styles: [
    `
      .search-row { display: flex; gap: 0.5rem; margin-bottom: 1rem; }
      .search-row input { flex: 1; padding: 0.5rem; }
      .search-row button { padding: 0.5rem 1rem; }
      .card-grid { display: grid; gap: 0.75rem; grid-template-columns: repeat(auto-fill, minmax(240px, 1fr)); }
      .card { border: 1px solid #ddd; border-radius: 8px; padding: 0.75rem; }
    `
  ]
})
export class PeoplePageComponent {
  private readonly http = inject(HttpClient);
  readonly people = signal<PersonItem[]>([]);
  readonly loading = signal(false);

  constructor() {
    this.search('');
  }

  search(query: string): void {
    this.loading.set(true);
    this.http.get<PersonItem[]>(`/api/people?query=${encodeURIComponent(query)}`).subscribe({
      next: (result) => {
        this.people.set(result ?? []);
        this.loading.set(false);
      },
      error: () => {
        this.people.set([]);
        this.loading.set(false);
      }
    });
  }
}
