export type UnauthorizedHandler = () => void;
export declare function setUnauthorizedHandler(handler: UnauthorizedHandler): void;
export declare function apiFetch(path: string, init?: RequestInit): Promise<Response>;
export declare function unwrap(res: Response): Promise<Response>;
export declare function jsonInit(method: string, body: unknown): RequestInit;
//# sourceMappingURL=http.d.ts.map