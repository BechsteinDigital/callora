/**
 * The public contract of `@callora/admin`.
 *
 * Everything reachable from this directory is what a plugin may rely on; everything else in this
 * project is the shell's own business and may change without notice. The subpath exports in
 * package.json point at the files next to this one, so a plugin writes
 * `import { registerPage } from '@callora/admin/extensions'` rather than reaching into `src/`.
 *
 * The package lives inside the module it describes — Umbraco does the same with
 * `@umbraco-cms/backoffice`. Two Vite configurations sit side by side: one builds the shell as an
 * application, one builds this directory as a library.
 */
/**
 * Contract version of this package. A plugin can compare it against what it was built for and
 * refuse an incompatible host instead of failing halfway through rendering.
 */
export declare const ADMIN_PACKAGE_VERSION = "0.1.0";
//# sourceMappingURL=index.d.ts.map