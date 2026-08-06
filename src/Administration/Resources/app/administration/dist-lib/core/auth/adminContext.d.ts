export interface AdminContext {
    userId: string;
    displayName: string | null;
    email: string | null;
    roles: string[];
    permissions: string[];
    scope: string | null;
    workspaceKey: string | null;
    isOperator: boolean;
}
export declare function parseAdminContext(raw: {
    userId: string;
    displayName?: string | null;
    email?: string | null;
    roles?: string[];
    permissions?: string[];
    scope?: string | null;
    workspaceKey?: string | null;
    isOperator?: boolean;
}): AdminContext;
//# sourceMappingURL=adminContext.d.ts.map