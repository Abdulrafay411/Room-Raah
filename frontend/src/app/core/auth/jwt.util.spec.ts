import { decodeJwt, isTokenExpired } from './jwt.util';

describe('jwt.util', () => {
  // Sample token with the exact claims shape from Brief §4.2:
  // role: 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role' = 'Seeker'
  // email_verified: 'true' (string!)
  // exp: 1788694705 (future)
  const validToken =
    'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.' +
    btoa(
      JSON.stringify({
        sub: '6',
        'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier': '6',
        email: 'seeker1@roomraah.local',
        jti: '4fec4d58-fd70-4e4c-8979-08b4d15dccca',
        email_verified: 'true',
        'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': 'Seeker',
        exp: 2500000000,
      })
    ) +
    '.signature';

  const unverifiedOwnerToken =
    'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.' +
    btoa(
      JSON.stringify({
        sub: '4',
        email: 'owner.new@roomraah.local',
        email_verified: 'false',
        'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': 'Owner',
        exp: 2500000000,
      })
    ) +
    '.signature';

  const expiredToken =
    'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.' +
    btoa(
      JSON.stringify({
        sub: '6',
        email: 'seeker1@roomraah.local',
        email_verified: 'true',
        role: 'Seeker',
        exp: 1000000000, // Year 2001
      })
    ) +
    '.signature';

  it('should decode role from long URI claim', () => {
    const decoded = decodeJwt(validToken);
    expect(decoded).toBeTruthy();
    expect(decoded?.role).toBe('Seeker');
    expect(decoded?.email).toBe('seeker1@roomraah.local');
  });

  it('should treat email_verified as string "true" or "false"', () => {
    const verifiedDecoded = decodeJwt(validToken);
    expect(verifiedDecoded?.email_verified).toBe('true');

    const unverifiedDecoded = decodeJwt(unverifiedOwnerToken);
    expect(unverifiedDecoded?.email_verified).toBe('false');
    expect(unverifiedDecoded?.role).toBe('Owner');
  });

  it('should correctly identify expired and valid tokens', () => {
    expect(isTokenExpired(validToken)).toBe(false);
    expect(isTokenExpired(expiredToken)).toBe(true);
    expect(isTokenExpired(null)).toBe(true);
    expect(isTokenExpired('')).toBe(true);
  });

  it('should return null on invalid or malformed tokens', () => {
    expect(decodeJwt('not.a.valid.jwt.token')).toBeNull();
    expect(decodeJwt('')).toBeNull();
  });
});
