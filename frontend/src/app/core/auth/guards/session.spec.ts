import { verifyParams } from './session';

describe('verifyParams', () => {
  it('carries the email, the way back and the reason', () => {
    expect(verifyParams({ email: 'a@roomraah.local', returnUrl: '/property/4', reason: 'save' })).toEqual({
      email: 'a@roomraah.local',
      returnUrl: '/property/4',
      reason: 'save',
    });
  });

  it('leaves out a way back that goes nowhere useful', () => {
    expect(verifyParams({ returnUrl: '/' })).toEqual({});
    expect(verifyParams({ returnUrl: '/verify?reason=save' })).toEqual({});
    expect(verifyParams({ returnUrl: null })).toEqual({});
  });
});
