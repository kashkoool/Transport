import { Pipe, PipeTransform, inject } from '@angular/core';
import { TranslationService } from './translation.service';

/**
 * `{{ 'nav.trips' | t }}` — translates a key to the active language. Impure so it re-evaluates when
 * the language toggles (a click triggers change detection app-wide in this zone-based app). Optional
 * params fill `{name}` placeholders: `{{ 'x.greeting' | t: { name: user() } }}`.
 */
@Pipe({ name: 't', standalone: true, pure: false })
export class TranslatePipe implements PipeTransform {
  private readonly i18n = inject(TranslationService);

  transform(key: string, params?: Record<string, string | number | null | undefined>): string {
    return this.i18n.t(key, params);
  }
}
