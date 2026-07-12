import type { ShellBlockExtension, ShellBlockExtensionEntry } from "#shell-core/types/shell-extensions";

function normalizeExtension(
  input: ShellBlockExtension,
  registrationOrder: number
): ShellBlockExtensionEntry | null {
  const blockName = input.blockName?.trim();
  const pluginId = input.pluginId?.trim();
  if (!blockName || !pluginId || (!input.contentHtml && !input.mount && !input.component)) {
    return null;
  }

  const mode = input.mode === "prepend" || input.mode === "replace" ? input.mode : "append";
  const priority = typeof input.priority === "number" && Number.isFinite(input.priority)
    ? Math.trunc(input.priority)
    : 0;

  return {
    ...input,
    blockName,
    pluginId,
    mode,
    priority,
    registrationOrder
  };
}

function byPrecedence(left: ShellBlockExtensionEntry, right: ShellBlockExtensionEntry): number {
  if (left.priority !== right.priority) {
    return right.priority - left.priority;
  }

  return left.registrationOrder - right.registrationOrder;
}

export interface ResolvedShellBlock {
  /**
   * Replace extensions ordered bottom-up: index 0 sits directly above the
   * block default, the last entry renders. Each level can embed the level
   * below via a `data-shell-parent` marker — Twig's parent().
   */
  replaceChain: ShellBlockExtensionEntry[];
  prepends: ShellBlockExtensionEntry[];
  appends: ShellBlockExtensionEntry[];
}

/**
 * Shared block registry for named page blocks: plugins register extensions
 * that prepend, append or replace the block default, ordered by priority.
 */
export function createShellBlockRegistry(stateKey: string) {
  const entries = useState<ShellBlockExtensionEntry[]>(stateKey, () => []);
  const knownBlocks = useState<string[]>(`${stateKey}-known`, () => []);

  function noteBlock(blockName: string): void {
    const name = blockName?.trim();
    if (name && !knownBlocks.value.includes(name)) {
      knownBlocks.value = [...knownBlocks.value, name].sort();
    }
  }

  function listKnownBlocks(): string[] {
    return [...knownBlocks.value];
  }

  function registerBlockExtension(input: ShellBlockExtension): void {
    const normalized = normalizeExtension(input, entries.value.length + 1);
    if (!normalized) {
      return;
    }

    const filtered = entries.value.filter((existing) =>
      existing.blockName !== normalized.blockName ||
      existing.pluginId !== normalized.pluginId ||
      existing.mode !== normalized.mode);

    filtered.push(normalized);
    entries.value = filtered;
  }

  function resolveBlock(blockName: string) {
    return computed<ResolvedShellBlock>(() => {
      const inBlock = entries.value.filter((entry) => entry.blockName === blockName);

      // Bottom-up: lower priority and earlier registration (= earlier in the
      // template load chain) sit closer to the block default.
      const replaceChain = inBlock
        .filter((entry) => entry.mode === "replace")
        .sort((left, right) => {
          if (left.priority !== right.priority) {
            return left.priority - right.priority;
          }

          return left.registrationOrder - right.registrationOrder;
        });

      return {
        replaceChain,
        prepends: inBlock.filter((entry) => entry.mode === "prepend").sort(byPrecedence),
        appends: inBlock.filter((entry) => entry.mode === "append").sort(byPrecedence)
      };
    });
  }

  return {
    entries: readonly(entries),
    registerBlockExtension,
    resolveBlock,
    noteBlock,
    listKnownBlocks
  };
}
