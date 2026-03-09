import { Component, input } from '@angular/core';

@Component({
  selector: 'otg-placeholder-page',
  standalone: true,
  template: `
    <h2>{{ title() }}</h2>
    <p>MVP placeholder route.</p>
  `
})
export class PlaceholderPageComponent {
  readonly title = input<string>('Coming Soon');
}
