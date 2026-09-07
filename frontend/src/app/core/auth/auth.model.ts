// Authentication models strictly adhering to RoomRaah OpenAPI & Brief specifications

export type UserRole = 'Seeker' | 'Owner' | 'Admin';

export interface AuthUserDto {
  id: number;
  fullName: string;
  email: string;
  role: UserRole;
  isEmailVerified: boolean;
}

export interface AuthSessionDto {
  accessToken: string;
  refreshToken: string;
  expiresInSeconds: number;
  user: AuthUserDto;
}

export interface RegisterRequest {
  fullName: string;
  email: string;
  phoneNumber: string;
  password: string;
  role: 'Seeker' | 'Owner';
  cnicNumber?: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface VerifyOtpRequest {
  email: string;
  code: string;
}

export interface ResendOtpRequest {
  email: string;
}

export interface RefreshTokenRequest {
  refreshToken: string;
}

export interface ForgotPasswordRequest {
  email: string;
}

export interface ResetPasswordRequest {
  email: string;
  token: string;
  newPassword: string;
}

export interface FieldErrorDto {
  field: string;
  message: string;
}

export interface ApiErrorResponse {
  success: boolean;
  statusCode: number;
  error: string;
  traceId?: string;
  errors?: FieldErrorDto[];
  retryAfterSeconds?: number;
}

// Decoded JWT token payload shape
export interface DecodedToken {
  sub: string;
  nameid: string; // 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'
  email: string;
  role: UserRole; // 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'
  email_verified: 'true' | 'false'; // Notice: string, not boolean!
  exp: number; // epoch seconds
  jti?: string;
  iss?: string;
  aud?: string;
}
