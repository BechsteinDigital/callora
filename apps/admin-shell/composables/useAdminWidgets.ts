import type { AdminWidget, AdminWidgetSlot } from "~/types/admin-plugin-extensions";

type AdminWidgetEntry = AdminWidget & {
  order: number;
  priority: number;
  registrationOrder: number;
};

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

function normalizeWidget(input: AdminWidget, registrationOrder: number): AdminWidgetEntry | null {
  const widgetKey = input.widgetKey?.trim();
  const pluginId = input.pluginId?.trim();
  const slot = input.slot?.trim() as AdminWidgetSlot;
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

function sortByPrecedence(entries: AdminWidgetEntry[]): AdminWidgetEntry[] {
  return entries
    .slice()
    .sort((left, right) => {
      if (left.priority !== right.priority) {
        return right.priority - left.priority;
      }

      return right.registrationOrder - left.registrationOrder;
    });
}

function sortByPlacement(entries: AdminWidgetEntry[]): AdminWidgetEntry[] {
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

function applyReplaceOverrides(baseWidgets: AdminWidgetEntry[], overrides: AdminWidgetEntry[]): AdminWidgetEntry[] {
  if (baseWidgets.length === 0 || overrides.length === 0) {
    return baseWidgets;
  }

  const replaceByTarget = new Map<string, AdminWidgetEntry>();
  for (const override of sortByPrecedence(overrides)) {
    const targetWidgetKey = override.override?.targetWidgetKey?.trim();
    if (!targetWidgetKey || replaceByTarget.has(targetWidgetKey)) {
      continue;
    }

    replaceByTarget.set(targetWidgetKey, override);
  }

  const output: AdminWidgetEntry[] = [];
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

function applyDecorators(baseWidgets: AdminWidgetEntry[], decorators: AdminWidgetEntry[]): AdminWidgetEntry[] {
  if (baseWidgets.length === 0 || decorators.length === 0) {
    return baseWidgets;
  }

  const beforeByTarget = new Map<string, AdminWidgetEntry[]>();
  const afterByTarget = new Map<string, AdminWidgetEntry[]>();

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

  for (const list of beforeByTarget.values()) {
    list.sort((left, right) => {
      if (left.priority !== right.priority) {
        return right.priority - left.priority;
      }

      return left.registrationOrder - right.registrationOrder;
    });
  }

  for (const list of afterByTarget.values()) {
    list.sort((left, right) => {
      if (left.priority !== right.priority) {
        return right.priority - left.priority;
      }

      return left.registrationOrder - right.registrationOrder;
    });
  }

  const output: AdminWidgetEntry[] = [];
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

function resolveWidgets(entries: AdminWidgetEntry[], slot: AdminWidgetSlot): AdminWidgetEntry[] {
  const inSlot = entries.filter((entry) => entry.slot === slot);
  if (inSlot.length === 0) {
    return [];
  }

  const byWidgetKey = new Map<string, AdminWidgetEntry>();
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

export function useAdminWidgets() {
  const entries = useState<AdminWidgetEntry[]>("admin-widgets", () => []);

  function registerWidget(input: AdminWidget): void {
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

  function listResolvedWidgets(slot: AdminWidgetSlot) {
    return computed(() => resolveWidgets(entries.value, slot));
  }

  return {
    entries: readonly(entries),
    registerWidget,
    listResolvedWidgets
  };
}
