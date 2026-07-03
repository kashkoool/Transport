import { Injectable, signal } from '@angular/core';

export type Theme = 'light' | 'dark';

/**
 * Light/dark theme. Persists the user's choice in localStorage and falls back to the OS preference
 * on first visit. Applies the `dark` class to <html>, which the Tailwind `dark:` variant keys off
 * (see the `@custom-variant dark` rule in styles.css).
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private static readonly STORAGE_KEY = 'tpx-theme';
  readonly theme = signal<Theme>(this.resolveInitial());

  constructor() {
    this.apply(this.theme());
  }

  toggle(): void {
    this.set(this.theme() === 'dark' ? 'light' : 'dark');
  }

  set(theme: Theme): void {
    this.theme.set(theme);
    try {
      localStorage.setItem(ThemeService.STORAGE_KEY, theme);
    } catch {
      // Private-mode / storage-disabled: theme still applies for the session, just doesn't persist.
    }
    this.apply(theme);
  }

  private resolveInitial(): Theme {
    let saved: string | null = null;
    try {
      saved = localStorage.getItem(ThemeService.STORAGE_KEY);
    } catch {
      saved = null;
    }
    if (saved === 'light' || saved === 'dark') return saved;
    return window.matchMedia?.('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  }

  private apply(theme: Theme): void {
    document.documentElement.classList.toggle('dark', theme === 'dark');
  }
}
