export interface RbacRole {
  role: string;
  permissions: string[];
}

export interface RbacUserRoleAssignment {
  userId: string;
  role: string;
}

export interface RbacPermission {
  permissionKey: string;
  function: string;
  action: string;
}

export interface UpsertRoleRequest {
  functions: Array<{
    function: string;
    actions: string[];
  }>;
}

export interface UpsertUserRoleRequest {
  role: string;
}
