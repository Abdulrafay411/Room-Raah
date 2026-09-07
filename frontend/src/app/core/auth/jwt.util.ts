import { DecodedToken, UserRole } from './auth.model';

const ROLE_CLAIM_KEY = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';
const NAME_IDENTIFIER_KEY = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier';

/**
 * Decodes a JWT token without external libraries.
 */
export function decodeJwt(token: string): DecodedToken | null {
  if (!token) return null;

  try {
    const parts = token.split('.');
    if (parts.length !== 3) return null;

    const base64Url = parts[1];
    const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
    const jsonPayload = decodeURIComponent(
      atob(base64)
        .split('')
        .map((c) => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
        .join('')
    );

    const raw = JSON.parse(jsonPayload);

    const role = (raw[ROLE_CLAIM_KEY] || raw['role'] || 'Seeker') as UserRole;
    const nameid = (raw[NAME_IDENTIFIER_KEY] || raw['nameid'] || raw['sub'] || '') as string;
    const emailVerified = String(raw['email_verified']).toLowerCase() === 'true' ? 'true' : 'false';

    return {
      sub: raw['sub'] || nameid,
      nameid,
      email: raw['email'] || '',
      role,
      email_verified: emailVerified,
      exp: typeof raw['exp'] === 'number' ? raw['exp'] : 0,
      jti: raw['jti'],
      iss: raw['iss'],
      aud: raw['aud'],
    };
  } catch {
    return null;
  }
}

/**
 * Checks if a JWT token is expired.
 * @param token The JWT access token
 * @param bufferSeconds Optional grace period in seconds (e.g. refresh 30s before actual expiry)
 */
export function isTokenExpired(token: string | null, bufferSeconds: number = 0): boolean {
  if (!token) return true;

  const decoded = decodeJwt(token);
  if (!decoded || !decoded.exp) return true;

  const currentEpoch = Math.floor(Date.now() / 1000);
  return decoded.exp - bufferSeconds <= currentEpoch;
}
