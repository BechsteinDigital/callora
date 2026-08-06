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
export declare const CAL_TOKENS: {
    readonly accent: "--cal-accent";
    readonly accentActive: "--cal-accent-active";
    readonly accentBorder: "--cal-accent-border";
    readonly accentContrast: "--cal-accent-contrast";
    readonly accentHover: "--cal-accent-hover";
    readonly accentSubtle: "--cal-accent-subtle";
    readonly bg: "--cal-bg";
    readonly bgSubtle: "--cal-bg-subtle";
    readonly border: "--cal-border";
    readonly borderStrong: "--cal-border-strong";
    readonly borderSubtle: "--cal-border-subtle";
    readonly colorAccent: "--cal-color-accent";
    readonly colorBg: "--cal-color-bg";
    readonly colorDanger: "--cal-color-danger";
    readonly colorMuted: "--cal-color-muted";
    readonly colorSurface: "--cal-color-surface";
    readonly colorText: "--cal-color-text";
    readonly contentMax: "--cal-content-max";
    readonly danger: "--cal-danger";
    readonly dangerBorder: "--cal-danger-border";
    readonly dangerHover: "--cal-danger-hover";
    readonly dangerSubtle: "--cal-danger-subtle";
    readonly durationBase: "--cal-duration-base";
    readonly durationFast: "--cal-duration-fast";
    readonly durationSlow: "--cal-duration-slow";
    readonly ease: "--cal-ease";
    readonly easeOut: "--cal-ease-out";
    readonly focusRing: "--cal-focus-ring";
    readonly font: "--cal-font";
    readonly fontMono: "--cal-font-mono";
    readonly info: "--cal-info";
    readonly infoBorder: "--cal-info-border";
    readonly infoSubtle: "--cal-info-subtle";
    readonly leadingNormal: "--cal-leading-normal";
    readonly leadingRelaxed: "--cal-leading-relaxed";
    readonly leadingTight: "--cal-leading-tight";
    readonly neutralBorder: "--cal-neutral-border";
    readonly neutralSubtle: "--cal-neutral-subtle";
    readonly overlayBackdrop: "--cal-overlay-backdrop";
    readonly radius: "--cal-radius";
    readonly radiusFull: "--cal-radius-full";
    readonly radiusLg: "--cal-radius-lg";
    readonly radiusMd: "--cal-radius-md";
    readonly radiusSm: "--cal-radius-sm";
    readonly radiusXl: "--cal-radius-xl";
    readonly radiusXs: "--cal-radius-xs";
    readonly shadowLg: "--cal-shadow-lg";
    readonly shadowMd: "--cal-shadow-md";
    readonly shadowSm: "--cal-shadow-sm";
    readonly shadowXl: "--cal-shadow-xl";
    readonly space: "--cal-space";
    readonly space1: "--cal-space-1";
    readonly space10: "--cal-space-10";
    readonly space12: "--cal-space-12";
    readonly space16: "--cal-space-16";
    readonly space2: "--cal-space-2";
    readonly space3: "--cal-space-3";
    readonly space4: "--cal-space-4";
    readonly space5: "--cal-space-5";
    readonly space6: "--cal-space-6";
    readonly space8: "--cal-space-8";
    readonly success: "--cal-success";
    readonly successBorder: "--cal-success-border";
    readonly successSubtle: "--cal-success-subtle";
    readonly surface: "--cal-surface";
    readonly surfaceActive: "--cal-surface-active";
    readonly surfaceHover: "--cal-surface-hover";
    readonly surfaceInset: "--cal-surface-inset";
    readonly surfaceRaised: "--cal-surface-raised";
    readonly text: "--cal-text";
    readonly text2xl: "--cal-text-2xl";
    readonly text3xl: "--cal-text-3xl";
    readonly textBase: "--cal-text-base";
    readonly textInverted: "--cal-text-inverted";
    readonly textLg: "--cal-text-lg";
    readonly textMd: "--cal-text-md";
    readonly textMuted: "--cal-text-muted";
    readonly textSecondary: "--cal-text-secondary";
    readonly textSm: "--cal-text-sm";
    readonly textXl: "--cal-text-xl";
    readonly textXs: "--cal-text-xs";
    readonly trackingWide: "--cal-tracking-wide";
    readonly warning: "--cal-warning";
    readonly warningBorder: "--cal-warning-border";
    readonly warningSubtle: "--cal-warning-subtle";
    readonly weightBold: "--cal-weight-bold";
    readonly weightMedium: "--cal-weight-medium";
    readonly weightNormal: "--cal-weight-normal";
    readonly weightSemibold: "--cal-weight-semibold";
    readonly zDropdown: "--cal-z-dropdown";
    readonly zModal: "--cal-z-modal";
    readonly zOverlay: "--cal-z-overlay";
    readonly zSticky: "--cal-z-sticky";
    readonly zToast: "--cal-z-toast";
};
export type CalTokenName = (typeof CAL_TOKENS)[keyof typeof CAL_TOKENS];
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
export declare function readToken(name: string, el?: Element): string;
//# sourceMappingURL=tokens.d.ts.map