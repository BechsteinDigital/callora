import { markRaw, reactive } from 'vue'
import {
  APPEARANCE_CONTROL_TYPES,
  type BlockCategory,
  type BlockDefinition,
  type ControlType,
} from './block-contract'

/**
 * Where blocks and their categories live.
 *
 * A block is a view with editor metadata, not a second kind of thing — registering one
 * also registers the view, so an island rendered server-side and a block placed in the
 * editor resolve to the same component. Two registries would be two chances to drift.
 *
 * There are no two classes of extender: the host's own blocks register through exactly
 * this contract.
 */
export interface BlockRegistry {
  readonly blocks: BlockDefinition[]
  readonly categories: BlockCategory[]
  /** Control types contributed by plugins, beyond the known ones. */
  readonly controlTypes: string[]
  registerBlock(block: BlockDefinition): void
  registerBlockCategory(category: BlockCategory): void
  registerControlType(type: string): void
}

/**
 * Why a registration was refused. Returned rather than thrown: one malformed block from
 * one plugin must not stop the surface from rendering the rest.
 */
export type BlockRegistrationProblem =
  | { kind: 'duplicate-block'; id: string }
  | { kind: 'duplicate-category'; id: string }
  | { kind: 'reserved-control-type'; type: string }
  | { kind: 'unknown-category'; blockId: string; category: string }

export interface BlockRegistryDiagnostics {
  readonly problems: BlockRegistrationProblem[]
}

export function createBlockRegistry(
  onView?: (block: BlockDefinition) => void,
): BlockRegistry & BlockRegistryDiagnostics {
  const blocks = reactive<BlockDefinition[]>([])
  const categories = reactive<BlockCategory[]>([])
  const controlTypes = reactive<string[]>([])
  const problems = reactive<BlockRegistrationProblem[]>([])

  return {
    blocks,
    categories,
    controlTypes,
    problems,

    registerBlock(block: BlockDefinition): void {
      if (blocks.some((existing) => existing.id === block.id)) {
        problems.push({ kind: 'duplicate-block', id: block.id })
        return
      }

      // A category that nobody registered is recorded and the block still appears —
      // under an unnamed group in the picker. Dropping it would punish the visitor for
      // a plugin's load order.
      if (!categories.some((existing) => existing.id === block.category)) {
        problems.push({
          kind: 'unknown-category',
          blockId: block.id,
          category: block.category,
        })
      }

      // markRaw: a component definition must not become a reactive proxy.
      const stored: BlockDefinition = {
        ...block,
        component: markRaw(block.component),
        ...(block.preview ? { preview: markRaw(block.preview) } : {}),
      }
      blocks.push(stored)
      blocks.sort((a, b) => (a.order ?? 0) - (b.order ?? 0))
      onView?.(stored)
    },

    registerBlockCategory(category: BlockCategory): void {
      if (categories.some((existing) => existing.id === category.id)) {
        problems.push({ kind: 'duplicate-category', id: category.id })
        return
      }

      categories.push(category)
      categories.sort((a, b) => (a.order ?? 0) - (b.order ?? 0))
    },

    registerControlType(type: string): void {
      // Appearance types are the guardrail: they pick from --cal-* roles and steps and
      // nothing else. A plugin that could contribute one would be able to hand the
      // editor a free colour picker, and every promise about a composed page still
      // looking like the product would be void.
      if ((APPEARANCE_CONTROL_TYPES as readonly string[]).includes(type)) {
        problems.push({ kind: 'reserved-control-type', type })
        return
      }

      if (!controlTypes.includes(type)) {
        controlTypes.push(type)
      }
    },
  }
}

/** Blocks offered for a surface, in registration order. */
export function blocksForSurface(
  registry: BlockRegistry,
  surface: 'surface' | 'admin',
): BlockDefinition[] {
  return registry.blocks.filter(
    (block) => !block.surfaces || block.surfaces.length === 0 || block.surfaces.includes(surface),
  )
}

/**
 * Context keys a block reads that nothing on this surface publishes. The editor shows
 * this while composing, so a block that would stay empty is caught before publishing
 * rather than by the visitor.
 */
export function unsatisfiedRequirements(
  registry: BlockRegistry,
  placedBlockIds: readonly string[],
): { blockId: string; missing: string[] }[] {
  const placed = registry.blocks.filter((block) => placedBlockIds.includes(block.id))
  const provided = new Set(placed.flatMap((block) => block.provides ?? []))

  return placed
    .map((block) => ({
      blockId: block.id,
      missing: (block.requires ?? []).filter((key) => !provided.has(key)),
    }))
    .filter((entry) => entry.missing.length > 0)
}

/** Whether a control type is one the host defines rather than a contributed one. */
export function isKnownControlType(type: ControlType): boolean {
  return KNOWN.has(type as string)
}

const KNOWN = new Set<string>([
  'text',
  'richText',
  'number',
  'toggle',
  'select',
  'list',
  'group',
  'media',
  'link',
  'date',
  'context',
  'query',
  ...APPEARANCE_CONTROL_TYPES,
  'slot',
])
