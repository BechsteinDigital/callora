import type { RouteLocationNormalizedLoaded } from 'vue-router'
import type { Breadcrumb } from './breadcrumb'

/**
 * Builds the topbar trail for a route.
 *
 * The trail is derived from route meta rather than from the URL: a segment like
 * `u-17` is an identifier, not a label, and guessing a title from a path is how
 * breadcrumbs end up reading "Users › U 17". A route declares its own title and,
 * when it sits inside a section, the parent it belongs to.
 */
export function breadcrumbsFor(route: RouteLocationNormalizedLoaded): Breadcrumb[] {
  const title = typeof route.meta.title === 'string' ? route.meta.title : null
  if (!title) {
    return [{ label: 'Übersicht' }]
  }

  const trail: Breadcrumb[] = []

  const parent = route.meta.parent
  if (isBreadcrumb(parent)) {
    trail.push({ label: parent.label, to: parent.to })
  }

  trail.push({ label: title })
  return trail
}

function isBreadcrumb(value: unknown): value is { label: string; to: string } {
  if (typeof value !== 'object' || value === null) {
    return false
  }
  const candidate = value as { label?: unknown; to?: unknown }
  return typeof candidate.label === 'string' && typeof candidate.to === 'string'
}
