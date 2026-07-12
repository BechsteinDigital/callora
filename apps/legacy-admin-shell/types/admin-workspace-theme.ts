export interface AdminWorkspaceThemeAssignment {
  workspaceKey: string;
  themePluginId: string | null;
  themeVersion: string | null;
  assignedBy: string | null;
  assignedAtUtc: string | null;
}

export interface AdminThemeDefinition {
  templateKey: string;
  surface: string;
  pluginId: string;
  version: string;
  displayName: string;
  templatePath: string;
  scope: string;
  isActive: boolean;
  priority: number;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface AdminWorkspaceThemeEffective {
  tenantKey: string;
  workspaceKey: string;
  templateKey: string;
  surface: string;
  pluginId: string;
  version: string;
  displayName: string;
  templatePath: string;
  scope: string;
  source: string;
  priority: number;
}

export interface AssignWorkspaceThemeRequest {
  themePluginId: string;
  themeVersion: string;
  assignedBy: string | null;
}

export interface AdminWorkspaceThemeSettingField {
  settingKey: string;
  label: string;
  fieldType: string;
  description: string | null;
  defaultValueJson: string | null;
  isRequired: boolean;
  sortOrder: number;
  groupName: string | null;
  optionsJson: string | null;
  isActive: boolean;
}

export interface AdminWorkspaceThemeSettings {
  workspaceKey: string;
  hasAssignedTheme: boolean;
  themePluginId: string | null;
  themeVersion: string | null;
  fields: AdminWorkspaceThemeSettingField[];
  valuesByKey: Record<string, string>;
}

export interface UpsertWorkspaceThemeSettingsRequest {
  valuesByKey: Record<string, unknown>;
}
