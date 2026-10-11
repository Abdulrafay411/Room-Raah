import { knownPassword, TEST_PASSWORD } from './test-accounts.service';

describe('knownPassword', () => {
  it('knows the password of seed and demo accounts only', () => {
    expect(knownPassword('sana.malik@roomraah.local')).toBe(TEST_PASSWORD);
    expect(knownPassword('Admin@RoomRaah.Local')).toBe(TEST_PASSWORD);
    expect(knownPassword('someone@gmail.com')).toBeNull();
    expect(knownPassword('roomraah.local@gmail.com')).toBeNull();
  });
});
