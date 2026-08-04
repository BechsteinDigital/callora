/**
 * Decides which sidebar entry is highlighted for the current path.
 *
 * Vue Router's own `router-link-active` cannot express this: it matches by
 * prefix, so the dashboard ("/") would light up on every single route, while a
 * detail route like `/users/u-1` would leave "Benutzer" dark under an exact
 * match. Both rules are needed, and which one applies depends on the entry.
 */
export function isNavItemActive(target: string, currentPath: string): boolean {
  const path = normalise(currentPath)
  const to = normalise(target)

  // The dashboard is the only entry that must match exactly — every other path
  // is nominally "below" it.
  if (to === '/') {
    return path === '/'
  }

  // A section stays highlighted while the operator is anywhere inside it, so
  // "Benutzer" remains lit on /users/new and /users/u-1.
  return path === to || path.startsWith(`${to}/`)
}

// Trailing slashes are equivalent for matching: /users/ and /users are one page.
function normalise(path: string): string {
  if (path.length > 1 && path.endsWith('/')) {
    return path.slice(0, -1)
  }
  return path
}
