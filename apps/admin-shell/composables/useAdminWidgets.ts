import type { AdminWidgetSlot } from "~/types/admin-plugin-extensions";

export function useAdminWidgets() {
  return createShellWidgetRegistry<AdminWidgetSlot>("admin-widgets");
}
