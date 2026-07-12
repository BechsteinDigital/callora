import type { AdminLoginNoticeExtension, AdminLoginNoticePosition } from "~/types/admin-login-extensions";

function sanitizeExtension(extension: AdminLoginNoticeExtension): AdminLoginNoticeExtension | null {
  const id = extension.id?.trim();
  const title = extension.title?.trim();

  if (!id || !title) {
    return null;
  }

  return {
    id,
    position: extension.position,
    title,
    description: extension.description?.trim(),
    color: extension.color ?? "info",
    icon: extension.icon?.trim(),
    to: extension.to?.trim(),
    target: extension.target ?? "_self",
    order: extension.order ?? 100
  };
}

export function useAdminLoginExtensions() {
  const extensions = useState<AdminLoginNoticeExtension[]>("admin-login-notice-extensions", () => []);

  function registerNoticeExtension(extension: AdminLoginNoticeExtension): void {
    const sanitized = sanitizeExtension(extension);
    if (!sanitized) {
      return;
    }

    const filtered = extensions.value.filter((entry) => entry.id !== sanitized.id);
    filtered.push(sanitized);
    extensions.value = filtered;
  }

  function getByPosition(position: AdminLoginNoticePosition): AdminLoginNoticeExtension[] {
    return extensions.value
      .filter((entry) => entry.position === position)
      .sort((left, right) => (left.order ?? 100) - (right.order ?? 100));
  }

  return {
    extensions: readonly(extensions),
    registerNoticeExtension,
    getByPosition
  };
}
