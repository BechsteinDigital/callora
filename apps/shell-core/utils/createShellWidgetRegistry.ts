import type { ShellWidget, ShellWidgetEntry } from "#shell-core/types/shell-extensions";

function normalizeOrder(value: number | undefined, fallback: number): number {
  if (typeof value !== "number" || !Number.isFinite(value)) {
    return fallback;
  }

  return Math.trunc(value);
}

function normalizePriority(value: number | undefined): number {
  if (typeof value !== "number" || !Number.isFinite(value)) {
    return 0;
  }

  return Math.trunc(value);
}

function normalizeWidget<TSlot extends string>(
  input: ShellWidget<TSlot>,
  registrationOrder: number
): ShellWidgetEntry<TSlot> | null {
  const widgetKey = input.widgetKey?.trim();
  const pluginId = input.pluginId?.trim();
  const slot = input.slot?.trim() as TSlot;
  const title = input.title?.trim();

  if (!widgetKey || !pluginId || !slot || !title) {
    return null;
  }

  return {
    widgetKey,
    pluginId,
    slot,
    title,
    description: input.description?.trim(),
    contentHtml: input.contentHtml,
    order: normalizeOrder(input.order, 1000),
    priority: normalizePriority(input.priority),
    override: input.override
      ? {
          targetWidgetKey: input.override.targetWidgetKey?.trim() || "",
          mode: input.override.mode
        }
      : undefined,
    registrationOrder
  };
}

function sortByPrecedence<TSlot extends string>(entries: ShellWidgetEntry<TSlot>[]): ShellWidgetEntry<TSlot>[] {
  return entries
    .slice()
    .sort((left, right) => {
      if (left.priority !== right.priority) {
        return right.priority - left.priority;
      }

      return right.registrationOrder - left.registrationOrder;
    });
}

function sortByPlacement<TSlot extends string>(entries: ShellWidgetEntry<TSlot>[]): ShellWidgetEntry<TSlot>[] {
  return entries
    .slice()
    .sort((left, right) => {
      if (left.order !== right.order) {
        return left.order - right.order;
      }

      if (left.priority !== right.priority) {
        return right.priority - left.priority;
      }

      return left.registrationOrder - right.registrationOrder;
    });
}

function applyReplaceOverrides<TSlot extends string>(
  baseWidgets: ShellWidgetEntry<TSlot>[],
  overrides: ShellWidgetEntry<TSlot>[]
): ShellWidgetEntry<TSlot>[] {
  if (baseWidgets.length === 0 || overrides.length === 0) {
    return baseWidgets;
  }

  const replaceByTarget = new Map<string, ShellWidgetEntry<TSlot>>();
  for (const override of sortByPrecedence(overrides)) {
    const targetWidgetKey = override.override?.targetWidgetKey?.trim();
    if (!targetWidgetKey || replaceByTarget.has(targetWidgetKey)) {
      continue;
    }

    replaceByTarget.set(targetWidgetKey, override);
  }

  const output: ShellWidgetEntry<TSlot>[] = [];
  for (const widget of baseWidgets) {
    const replacement = replaceByTarget.get(widget.widgetKey);
    if (!replacement) {
      output.push(widget);
      continue;
    }

    output.push({
      ...replacement,
      order: normalizeOrder(replacement.order, normalizeOrder(widget.order, 1000)),
      override: undefined
    });
  }

  return output;
}

function applyDecorators<TSlot extends string>(
  baseWidgets: ShellWidgetEntry<TSlot>[],
  decorators: ShellWidgetEntry<TSlot>[]
): ShellWidgetEntry<TSlot>[] {
  if (baseWidgets.length === 0 || decorators.length === 0) {
    return baseWidgets;
  }

  const beforeByTarget = new Map<string, ShellWidgetEntry<TSlot>[]>();
  const afterByTarget = new Map<string, ShellWidgetEntry<TSlot>[]>();

  for (const decorator of decorators) {
    const targetWidgetKey = decorator.override?.targetWidgetKey?.trim();
    if (!targetWidgetKey) {
      continue;
    }

    const bucket = decorator.override?.mode === "before" ? beforeByTarget : afterByTarget;
    const list = bucket.get(targetWidgetKey) || [];
    list.push(decorator);
    bucket.set(targetWidgetKey, list);
  }

  const sortDecorators = (list: ShellWidgetEntry<TSlot>[]) => {
    list.sort((left, right) => {
      if (left.priority !== right.priority) {
        return right.priority - left.priority;
      }

      return left.registrationOrder - right.registrationOrder;
    });
  };

  for (const list of beforeByTarget.values()) {
    sortDecorators(list);
  }

  for (const list of afterByTarget.values()) {
    sortDecorators(list);
  }

  const output: ShellWidgetEntry<TSlot>[] = [];
  for (const widget of baseWidgets) {
    const before = beforeByTarget.get(widget.widgetKey) || [];
    for (const entry of before) {
      output.push({ ...entry, override: undefined });
    }

    output.push(widget);

    const after = afterByTarget.get(widget.widgetKey) || [];
    for (const entry of after) {
      output.push({ ...entry, override: undefined });
    }
  }

  return output;
}

function resolveWidgets<TSlot extends string>(
  entries: ShellWidgetEntry<TSlot>[],
  slot: TSlot
): ShellWidgetEntry<TSlot>[] {
  const inSlot = entries.filter((entry) => entry.slot === slot);
  if (inSlot.length === 0) {
    return [];
  }

  const byWidgetKey = new Map<string, ShellWidgetEntry<TSlot>>();
  for (const entry of sortByPrecedence(inSlot.filter((entry) => !entry.override))) {
    if (!byWidgetKey.has(entry.widgetKey)) {
      byWidgetKey.set(entry.widgetKey, entry);
    }
  }

  const baseWidgets = sortByPlacement(Array.from(byWidgetKey.values()));
  const replaceOverrides = inSlot.filter((entry) => entry.override?.mode === "replace");
  const replaced = applyReplaceOverrides(baseWidgets, replaceOverrides);
  const decorateOverrides = inSlot.filter((entry) => {
    const mode = entry.override?.mode;
    return mode === "before" || mode === "after";
  });

  return applyDecorators(replaced, decorateOverrides);
}

/**
 * Shared widget registry for shell extension slots: plugins register widgets,
 * pages resolve them per slot including replace/before/after overrides with
 * priority precedence.
 */
export function createShellWidgetRegistry<TSlot extends string>(stateKey: string) {
  const entries = useState<ShellWidgetEntry<TSlot>[]>(stateKey, () => []);

  function registerWidget(input: ShellWidget<TSlot>): void {
    const normalized = normalizeWidget(input, entries.value.length + 1);
    if (!normalized) {
      return;
    }

    const filtered = entries.value.filter((existing) => {
      if (existing.widgetKey !== normalized.widgetKey) {
        return true;
      }

      if (existing.pluginId !== normalized.pluginId) {
        return true;
      }

      if (existing.slot !== normalized.slot) {
        return true;
      }

      const existingTarget = existing.override?.targetWidgetKey ?? "";
      const normalizedTarget = normalized.override?.targetWidgetKey ?? "";
      return existingTarget !== normalizedTarget;
    });

    filtered.push(normalized);
    entries.value = filtered;
  }

  function listResolvedWidgets(slot: TSlot) {
    return computed(() => resolveWidgets(entries.value, slot));
  }

  return {
    entries: readonly(entries),
    registerWidget,
    listResolvedWidgets
  };
}
