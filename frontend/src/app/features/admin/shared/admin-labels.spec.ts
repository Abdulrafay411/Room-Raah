import { describe, expect, it } from 'vitest';
import { fieldLabel, isRequiredCheck, sentenceCase, statusLabel, statusTone } from './admin-labels';

describe('admin status labels', () => {
  it('turns enum names into words', () => {
    expect(statusLabel('PendingReview')).toBe('Pending review');
    expect(statusLabel('NotChecked')).toBe('Not checked');
    expect(statusLabel('UnderReview')).toBe('Under review');
  });

  it('gives each status a tone the pill can colour by', () => {
    expect(statusTone('Published')).toBe('good');
    expect(statusTone('Rejected')).toBe('bad');
    expect(statusTone('PendingReview')).toBe('warn');
    expect(statusTone('Draft')).toBe('neutral');
  });

  it('never prints a raw enum for a status it has not met', () => {
    expect(statusLabel('SomethingNewFromTheApi')).toBe('Something new from the api');
    expect(statusTone('SomethingNewFromTheApi')).toBe('neutral');
  });

  it('handles nothing', () => {
    expect(statusLabel(null)).toBe('');
    expect(statusTone(undefined)).toBe('neutral');
  });
});

describe('verification checks', () => {
  it('knows which four gate publishing', () => {
    expect(isRequiredCheck('OwnerIdentity')).toBe(true);
    expect(isRequiredCheck('Availability')).toBe(false);
  });
});

describe('revision field labels', () => {
  it('uses the words the owner saw on the form', () => {
    expect(fieldLabel('monthlyRent')).toBe('Monthly rent');
    expect(fieldLabel('genderPolicy')).toBe('Who it is for');
  });

  it('falls back to sentence case', () => {
    expect(fieldLabel('someNewField')).toBe('Some new field');
    expect(sentenceCase('owner_identity')).toBe('Owner identity');
  });
});
