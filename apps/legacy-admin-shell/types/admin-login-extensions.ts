export type AdminLoginNoticePosition = "before-form" | "after-form";

export interface AdminLoginNoticeExtension {
  id: string;
  position: AdminLoginNoticePosition;
  title: string;
  description?: string;
  color?: "info" | "success" | "warning" | "error" | "neutral";
  icon?: string;
  to?: string;
  target?: "_blank" | "_self";
  order?: number;
}
