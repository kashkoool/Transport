import { DOCUMENT, Injectable, PLATFORM_ID, computed, effect, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { Lang, dictionaries } from './dictionaries';

const STORAGE_KEY = 'tpx-lang';

/**
 * Dependency-free bilingual (English + Arabic) i18n. Holds the active language as a signal, mirrors
 * it onto `<html lang dir>` (so RTL and the browser's own direction handling kick in), and persists
 * the choice. Look-ups fall back to the key, then to English, so a missing Arabic string degrades
 * gracefully rather than showing blank.
 */
@Injectable({ providedIn: 'root' })
export class TranslationService {
  private readonly doc = inject(DOCUMENT);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly _lang = signal<Lang>(this.initial());

  readonly lang = this._lang.asReadonly();
  readonly dir = computed<'rtl' | 'ltr'>(() => (this._lang() === 'ar' ? 'rtl' : 'ltr'));
  readonly isRtl = computed(() => this._lang() === 'ar');

  constructor() {
    // Keep <html> and storage in sync with the active language. Uses the injected DOCUMENT so it
    // works during prerender too (setting lang/dir on the static HTML); storage is browser-only.
    effect(() => {
      const lang = this._lang();
      const html = this.doc.documentElement;
      html.setAttribute('lang', lang);
      html.setAttribute('dir', lang === 'ar' ? 'rtl' : 'ltr');
      if (!this.isBrowser) return;
      try {
        localStorage.setItem(STORAGE_KEY, lang);
      } catch {
        // storage unavailable (private mode) — the in-memory signal still drives the UI.
      }
    });
  }

  /** Translate a key, filling `{name}` placeholders. Falls back to English, then the raw key. */
  t(key: string, params?: Record<string, string | number | null | undefined>): string {
    const lang = this._lang();
    const value = dictionaries[lang][key] ?? dictionaries.en[key] ?? key;
    if (!params) return value;
    return value.replace(/\{(\w+)\}/g, (_, name: string) => String(params[name] ?? `{${name}}`));
  }

  set(lang: Lang): void {
    this._lang.set(lang);
  }

  toggle(): void {
    this._lang.update((l) => (l === 'ar' ? 'en' : 'ar'));
  }

  private initial(): Lang {
    // Prerender (Node): no storage / navigator — ship English in the static HTML (lang="en").
    if (!this.isBrowser) return 'en';
    try {
      const stored = localStorage.getItem(STORAGE_KEY);
      if (stored === 'ar' || stored === 'en') return stored;
    } catch {
      // ignore
    }
    // Default to Arabic when the browser prefers it, else English.
    return navigator.language?.toLowerCase().startsWith('ar') ? 'ar' : 'en';
  }
}
