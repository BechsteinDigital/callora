import type { WorkspaceWidgetSlot } from "~/types/workspace-plugin-extensions";

export function useWorkspaceWidgets() {
  return createShellWidgetRegistry<WorkspaceWidgetSlot>("workspace-widgets");
}
