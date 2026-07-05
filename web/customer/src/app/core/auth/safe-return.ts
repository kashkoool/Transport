/**
 * Guards against open-redirect: returns `url` only when it is a safe app-relative path,
 * otherwise `fallback`. A path is safe when it starts with a single `/` (not `//`, which is
 * protocol-relative and would escape to another host) and contains no `\` or `:` (which could
 * smuggle a scheme or backslash-based host). Everything else — absolute URLs, `javascript:`,
 * `null` — falls back.
 */
export function safeReturnPath(url: string | null, fallback: string): string {
  if (
    !!url &&
    url.startsWith('/') &&
    !url.startsWith('//') &&
    !url.includes('\\') &&
    !url.includes(':')
  ) {
    return url;
  }
  return fallback;
}
