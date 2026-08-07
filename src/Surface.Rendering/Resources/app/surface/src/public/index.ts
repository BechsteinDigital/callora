/**
 * `@callora/surface` — what a surface plugin compiles against.
 *
 * This directory IS the contract. Everything else under `src/` is the runtime's own
 * business, reachable inside the project but not through a package entry point.
 *
 * The contract used to live in a separate package (`custom/surface-sdk`) that restated
 * every type the runtime already had, because the runtime was private. Two declarations
 * of one contract can only drift; now the runtime is the package, and there is one.
 */

// ── Context: where a view is, and who is looking ─────────────────────────────
export type {
  SurfaceCaller,
  SurfaceContext,
  SurfaceSubject,
} from '../surface-context'

// ── Views: what a plugin contributes ─────────────────────────────────────────
export type {
  SurfaceRegistry,
  SurfaceView,
  SurfaceViewParams,
  SurfaceViewProps,
} from '../surface-registry'

// ── Blocks: a view with editor metadata ──────────────────────────────────────
export type {
  AppearanceControlType,
  Binding,
  BlockCategory,
  BlockControl,
  BlockControls,
  BlockDefinition,
  BlockSurface,
  ContentControlType,
  ControlOption,
  ControlType,
  KnownControlType,
  SourceControlType,
  StructureControlType,
} from '../blocks/block-contract'

export type { BlockRegistry } from '../blocks/block-registry'

// ── The context channel islands collaborate over ─────────────────────────────
export type {
  SurfaceContextCardinality,
  SurfaceContextChannel,
  SurfaceContextDescriptor,
  SurfaceContextKeyDiagnostics,
  SurfaceContextPublisher,
} from '../surface-context-channel'

export {
  createSurfaceContextScope,
  surfaceContextChannel,
  type SurfaceContextScope,
} from './context'

// ── Registration ─────────────────────────────────────────────────────────────
export {
  registerBlock,
  registerBlockCategory,
  registerControlType,
  registerSurfaceView,
} from './register'

// ── Loading a surface's bundles, for hosts that are not the surface ──────────
export {
  ensureSurfaceRegistry,
  loadSurfaceBundles,
  type SurfaceBundleLoad,
  type SurfaceBundleOptions,
} from './bundles'

export type { PluginLoadResult } from '../plugin-loader'
