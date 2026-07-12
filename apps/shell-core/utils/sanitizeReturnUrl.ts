/**
 * Restricts login return targets to same-origin relative paths: they must
 * start with a single "/" and must not be protocol-relative ("//…") or
 * backslash-escaped ("/\…"), otherwise the fallback is used — prevents open
 * redirects via ?returnUrl=.
 */
export function sanitizeReturnUrl(candidate: unknown, fallback: string): string {
  if (typeof candidate !== "string") {
    return fallback;
  }

  const trimmed = candidate.trim();
  return /^\/(?![/\\])/.test(trimmed) ? trimmed : fallback;
}
