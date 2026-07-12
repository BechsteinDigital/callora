export interface PluginInstallationSummary {
  pluginId: string;
  displayName: string;
  assemblyPath: string;
  entryTypeName: string | null;
  state: number;
  installedAtUtc: string;
  updatedAtUtc: string;
}

export interface PluginLifecycleApiResponse {
  isSuccess: boolean;
  pluginId: string | null;
  message: string | null;
  errorCode: string | null;
  warningMessage: string | null;
  warningCode: string | null;
}

export interface InstallNuGetPluginRequest {
  packageId: string;
  packageVersion: string;
  assemblyFileName: string | null;
  entryTypeName: string | null;
  requestedBy: string | null;
}

export interface InstallLocalPluginRequest {
  pluginId: string;
  buildIfNeeded: boolean;
  forceBuild: boolean;
  requestedBy: string | null;
}

export interface PluginAuditEntry {
  occurredAtUtc: string;
  action: string;
  pluginId: string | null;
  isSuccess: boolean;
  requestedBy: string | null;
  message: string | null;
  metadata: Record<string, string> | null;
}

export interface PluginContractSupport {
  contractVersion: string;
  supportStatus: string;
  isInstallable: boolean;
  emitsWarning: boolean;
  message: string;
}

export interface PluginContractCompatibility {
  hostVersion: string;
  coreVersion: string;
  contractVersion: string;
  result: string;
  isCompatible: boolean;
  message: string;
}

export interface TrustedPluginSigner {
  publisherId: string;
  displayName: string;
  thumbprint: string;
  source: string;
}

export interface PluginEntitlementStatus {
  workspaceKey: string;
  pluginId: string;
  isEntitled: boolean;
  tenantKey: string | null;
}
