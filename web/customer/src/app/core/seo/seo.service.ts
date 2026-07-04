import { DOCUMENT, inject, Injectable } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { environment } from '../../../environments/environment';

/** Metadata for a single page. `path` is app-relative (e.g. `/bus/damascus-to-aleppo`). */
export interface SeoMeta {
  title: string;
  description: string;
  /** App-relative path for the canonical + og:url. Defaults to the current location path. */
  path?: string;
  /** App-relative or absolute image for OG/Twitter cards. Defaults to /og-default.png. */
  image?: string;
  /** og:type — 'website' (default) for listings, 'article' for guides, 'product' for a route page. */
  type?: string;
}

/**
 * Central place to emit crawler-facing metadata. Because pages are prerendered (SSG), whatever
 * these methods write into <head> at component init is baked into the static HTML a bot receives.
 *
 * Uses Angular's Title/Meta so it works identically on the server (prerender) and in the browser.
 * The DomSanitizer-free JSON-LD path appends a <script type="application/ld+json"> element — the
 * JSON is always app-controlled (never user input), so it is safe to inject verbatim.
 */
@Injectable({ providedIn: 'root' })
export class SeoService {
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);
  private readonly doc = inject(DOCUMENT);

  private readonly base = (environment.siteBaseUrl || '').replace(/\/$/, '');
  private readonly siteName = 'TPX Travel';
  private readonly defaultImage = '/og-default.png';

  /** Apply a full indexable metadata set for a public page. */
  set(m: SeoMeta): void {
    const path = m.path ?? this.currentPath();
    const url = this.abs(path);
    const image = this.abs(m.image ?? this.defaultImage);
    const type = m.type ?? 'website';

    this.title.setTitle(m.title);
    this.meta.updateTag({ name: 'description', content: m.description });

    // A public page is indexable — remove any stale noindex from a prior navigation.
    this.meta.updateTag({ name: 'robots', content: 'index,follow' });

    this.setCanonical(url);

    // Open Graph
    this.meta.updateTag({ property: 'og:title', content: m.title });
    this.meta.updateTag({ property: 'og:description', content: m.description });
    this.meta.updateTag({ property: 'og:url', content: url });
    this.meta.updateTag({ property: 'og:type', content: type });
    this.meta.updateTag({ property: 'og:image', content: image });
    this.meta.updateTag({ property: 'og:site_name', content: this.siteName });
    this.meta.updateTag({ property: 'og:locale', content: 'en_US' });

    // Twitter card
    this.meta.updateTag({ name: 'twitter:card', content: 'summary_large_image' });
    this.meta.updateTag({ name: 'twitter:title', content: m.title });
    this.meta.updateTag({ name: 'twitter:description', content: m.description });
    this.meta.updateTag({ name: 'twitter:image', content: image });
  }

  /**
   * Mark the current page as non-indexable (auth/admin/private/404). Emits
   * <meta name="robots" content="noindex,follow"> and drops the canonical so a private page
   * can never be indexed even when prerendered/served at HTTP 200.
   */
  noindex(): void {
    this.meta.updateTag({ name: 'robots', content: 'noindex,follow' });
    this.removeCanonical();
  }

  /**
   * Inject one JSON-LD block. `id` de-dupes by @type so re-navigating to the same page kind
   * replaces rather than stacks blocks. Safe: `data` is always app-generated, never user input.
   */
  setJsonLd(id: string, data: unknown): void {
    const elementId = `ld-${id}`;
    let script = this.doc.getElementById(elementId) as HTMLScriptElement | null;
    if (!script) {
      script = this.doc.createElement('script');
      script.type = 'application/ld+json';
      script.id = elementId;
      this.doc.head.appendChild(script);
    }
    script.textContent = JSON.stringify(data);
  }

  /** Resolve an app-relative path (or pass through an absolute URL) to an absolute URL. */
  abs(pathOrUrl: string): string {
    if (/^https?:\/\//i.test(pathOrUrl)) return pathOrUrl;
    const path = pathOrUrl.startsWith('/') ? pathOrUrl : `/${pathOrUrl}`;
    return `${this.base}${path}`;
  }

  private setCanonical(url: string): void {
    let link = this.doc.head.querySelector<HTMLLinkElement>('link[rel="canonical"]');
    if (!link) {
      link = this.doc.createElement('link');
      link.setAttribute('rel', 'canonical');
      this.doc.head.appendChild(link);
    }
    link.setAttribute('href', url);
  }

  private removeCanonical(): void {
    const link = this.doc.head.querySelector('link[rel="canonical"]');
    if (link) link.remove();
  }

  private currentPath(): string {
    try {
      return this.doc.location?.pathname || '/';
    } catch {
      return '/';
    }
  }
}
