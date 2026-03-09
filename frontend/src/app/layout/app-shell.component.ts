import { Component, computed, inject } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { AuthStateService } from '../core/auth/auth-state.service';
import { NAV_ITEMS } from '../core/nav/nav.config';

@Component({
  selector: 'otg-app-shell',
  standalone: true,
  imports: [RouterLink, RouterOutlet],
  template: `
    <header class="otg-header">
      <h1>Off the Grid</h1>
    </header>

    <div class="otg-layout">
      <nav class="otg-nav">
        @for (item of visibleNav(); track item.route) {
          <a [routerLink]="item.route">{{ item.label }}</a>
        }
      </nav>

      <main class="otg-main">
        <router-outlet />
      </main>
    </div>
  `,
  styles: [
    `
      .otg-header { padding: 1rem; border-bottom: 1px solid #ddd; }
      .otg-layout { display: grid; grid-template-columns: 240px 1fr; min-height: calc(100vh - 72px); }
      .otg-nav { display: flex; flex-direction: column; gap: 0.75rem; padding: 1rem; border-right: 1px solid #ddd; }
      .otg-main { padding: 1rem; }
      @media (max-width: 768px) {
        .otg-layout { grid-template-columns: 1fr; }
        .otg-nav { border-right: none; border-bottom: 1px solid #ddd; }
      }
    `
  ]
})
export class AppShellComponent {
  private readonly authState = inject(AuthStateService);

  readonly visibleNav = computed(() => {
    const userRoles = this.authState.user()?.roles ?? [];
    return NAV_ITEMS.filter((item) => item.roles.some((role) => userRoles.includes(role)));
  });
}
