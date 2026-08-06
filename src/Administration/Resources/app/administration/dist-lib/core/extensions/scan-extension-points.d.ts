/**
 * Extracts the extension points a source file declares.
 *
 * Pure by design: the generator (`bin/generate-catalog.mjs`) walks the files, this reads them —
 * so the interesting part is testable without a filesystem.
 *
 * Deliberately regex-based rather than AST-based. The two call shapes are fixed by convention
 * (`<ExtensionSlot name="…">` and `runHook('…')`), a parser would pull in a heavy dependency for
 * no gain, and a missed point is caught by the catalog test rather than shipping silently.
 */
export type ExtensionPointKind = 'slot' | 'hook';
export interface ExtensionPoint {
    readonly kind: ExtensionPointKind;
    /** Dotted name, or a `*`-suffixed pattern when the call interpolates. */
    readonly name: string;
    /** Path of the declaring file, relative to the scan root. */
    readonly file: string;
    /** True when the name is assembled at runtime and only its prefix is known. */
    readonly dynamic?: boolean;
}
export declare function scanExtensionPoints(rawSource: string, file: string): ExtensionPoint[];
//# sourceMappingURL=scan-extension-points.d.ts.map