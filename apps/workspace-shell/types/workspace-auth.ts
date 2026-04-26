export type WorkspaceAuthSession = {
  userId: string;
  displayName: string | null;
  email: string | null;
  role: string | null;
  workspaceKey: string | null;
};

export type WorkspaceAuthMeResponse = {
  userId: string;
  displayName: string | null;
  email: string | null;
  role: string | null;
};

export type WorkspaceLoginRequest = {
  login: string;
  password: string;
  workspaceKey: string;
};

export type WorkspaceLoginResponse = {
  accessToken: string;
  tokenType: string;
  expiresInSeconds: number;
  userId: string;
  displayName: string | null;
  email: string | null;
  role: string | null;
  workspaceKey: string | null;
};
