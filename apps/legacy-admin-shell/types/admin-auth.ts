export interface AdminLoginRequest {
  login: string;
  password: string;
}

export interface AdminLoginResponse {
  accessToken: string;
  tokenType: string;
  expiresInSeconds: number;
  userId: string;
  displayName: string | null;
  email: string | null;
  role: string | null;
  workspaceKey: string | null;
}

export interface AdminAuthSession {
  userId: string;
  displayName: string | null;
  email: string | null;
  role: string | null;
}

export interface AdminAuthMeResponse {
  userId: string;
  displayName: string | null;
  email: string | null;
  role: string | null;
}
