/**
 * The `--cal-*` names as TypeScript constants.
 *
 * They exist because a plugin bundle does not compile against our SCSS — it can only reach
 * runtime custom properties. Renaming one of these breaks foreign plugin UIs, so the names are
 * public contract; `tokens.scss` says the same thing from the other side.
 *
 * Only the NAMES live here, never the values: the values belong to the active theme, and a
 * block that hard-codes one has left the token system.
 *
 * The list is checked against tokens.scss by tokens.spec.ts, so a token added to the stylesheet
 * without a constant (or the reverse) fails the build rather than drifting quietly.
 */
export const CAL_TOKENS = {
  // accent
  accent: '--cal-accent',
  accentActive: '--cal-accent-active',
  accentBorder: '--cal-accent-border',
  accentContrast: '--cal-accent-contrast',
  accentHover: '--cal-accent-hover',
  accentSubtle: '--cal-accent-subtle',
  // bg
  bg: '--cal-bg',
  bgSubtle: '--cal-bg-subtle',
  // border
  border: '--cal-border',
  borderStrong: '--cal-border-strong',
  borderSubtle: '--cal-border-subtle',
  // color
  colorAccent: '--cal-color-accent',
  colorBg: '--cal-color-bg',
  colorDanger: '--cal-color-danger',
  colorMuted: '--cal-color-muted',
  colorSurface: '--cal-color-surface',
  colorText: '--cal-color-text',
  // content
  contentMax: '--cal-content-max',
  // danger
  danger: '--cal-danger',
  dangerBorder: '--cal-danger-border',
  dangerHover: '--cal-danger-hover',
  dangerSubtle: '--cal-danger-subtle',
  // duration
  durationBase: '--cal-duration-base',
  durationFast: '--cal-duration-fast',
  durationSlow: '--cal-duration-slow',
  // ease
  ease: '--cal-ease',
  easeOut: '--cal-ease-out',
  // focus
  focusRing: '--cal-focus-ring',
  // font
  font: '--cal-font',
  fontMono: '--cal-font-mono',
  // info
  info: '--cal-info',
  infoBorder: '--cal-info-border',
  infoSubtle: '--cal-info-subtle',
  // leading
  leadingNormal: '--cal-leading-normal',
  leadingRelaxed: '--cal-leading-relaxed',
  leadingTight: '--cal-leading-tight',
  // neutral
  neutralBorder: '--cal-neutral-border',
  neutralSubtle: '--cal-neutral-subtle',
  // overlay
  overlayBackdrop: '--cal-overlay-backdrop',
  // radius
  radius: '--cal-radius',
  radiusFull: '--cal-radius-full',
  radiusLg: '--cal-radius-lg',
  radiusMd: '--cal-radius-md',
  radiusSm: '--cal-radius-sm',
  radiusXl: '--cal-radius-xl',
  radiusXs: '--cal-radius-xs',
  // shadow
  shadowLg: '--cal-shadow-lg',
  shadowMd: '--cal-shadow-md',
  shadowSm: '--cal-shadow-sm',
  shadowXl: '--cal-shadow-xl',
  // space
  space: '--cal-space',
  space1: '--cal-space-1',
  space10: '--cal-space-10',
  space12: '--cal-space-12',
  space16: '--cal-space-16',
  space2: '--cal-space-2',
  space3: '--cal-space-3',
  space4: '--cal-space-4',
  space5: '--cal-space-5',
  space6: '--cal-space-6',
  space8: '--cal-space-8',
  // success
  success: '--cal-success',
  successBorder: '--cal-success-border',
  successSubtle: '--cal-success-subtle',
  // surface
  surface: '--cal-surface',
  surfaceActive: '--cal-surface-active',
  surfaceHover: '--cal-surface-hover',
  surfaceInset: '--cal-surface-inset',
  surfaceRaised: '--cal-surface-raised',
  // text
  text: '--cal-text',
  text2xl: '--cal-text-2xl',
  text3xl: '--cal-text-3xl',
  textBase: '--cal-text-base',
  textInverted: '--cal-text-inverted',
  textLg: '--cal-text-lg',
  textMd: '--cal-text-md',
  textMuted: '--cal-text-muted',
  textSecondary: '--cal-text-secondary',
  textSm: '--cal-text-sm',
  textXl: '--cal-text-xl',
  textXs: '--cal-text-xs',
  // tracking
  trackingWide: '--cal-tracking-wide',
  // warning
  warning: '--cal-warning',
  warningBorder: '--cal-warning-border',
  warningSubtle: '--cal-warning-subtle',
  // weight
  weightBold: '--cal-weight-bold',
  weightMedium: '--cal-weight-medium',
  weightNormal: '--cal-weight-normal',
  weightSemibold: '--cal-weight-semibold',
  // z
  zDropdown: '--cal-z-dropdown',
  zModal: '--cal-z-modal',
  zOverlay: '--cal-z-overlay',
  zSticky: '--cal-z-sticky',
  zToast: '--cal-z-toast',
} as const

export type CalTokenName = (typeof CAL_TOKENS)[keyof typeof CAL_TOKENS]

/**
 * Reads the effective value of a token.
 *
 * Scoped to an element rather than to the document, because the composer canvas sets a target
 * surface's theme on a container instead of on `:root` — the same lookup then works inside and
 * outside the editor.
 *
 * An unset token yields an empty string rather than throwing: a missing token is a themed
 * default, not an error.
 */
export function readToken(name: string, el: Element = document.documentElement): string {
  return getComputedStyle(el).getPropertyValue(name).trim()
}
