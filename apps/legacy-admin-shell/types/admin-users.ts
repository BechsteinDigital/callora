export interface AdminUser {
  externalId: string;
  email: string | null;
  displayName: string | null;
  hasPassword: boolean;
  passwordHashAlgorithm: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface CreateAdminUserRequest {
  externalId: string;
  email: string | null;
  displayName: string | null;
  password: string;
}

export interface UpdateAdminUserRequest {
  email: string | null;
  displayName: string | null;
  password: string | null;
}
