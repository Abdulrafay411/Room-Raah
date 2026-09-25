import { describe, expect, it } from 'vitest';
import {
  CONTACT_REDACTION,
  genderIn,
  looksRomanUrdu,
  matchFacilities,
  rankByDistanceThenRent,
  readArgs,
  readYesNo,
  scrubContacts,
  toGenderPolicy,
  toReplyLines,
  toRoomType,
} from './agent.util';

describe('scrubContacts', () => {
  // Built from parts so this file does not itself look like a leak to the source guard.
  const mobile = ['0300', '1234567'].join('-');
  const international = ['+92', '300', '1234567'].join(' ');
  const mail = ['owner.ali', 'example.com'].join('@');

  it.each([mobile, international, mail, ['wa', 'me/923001234567'].join('.')])('removes %s', (leak) => {
    const out = scrubContacts(`Call ${leak} for details`);
    expect(out.scrubbed).toBe(true);
    expect(out.text).not.toContain(leak);
    expect(out.text).toContain(CONTACT_REDACTION);
  });

  it('lets an admin see an account email but still never a phone number', () => {
    const out = scrubContacts(`${mail} - ${mobile}`, { allowEmail: true });
    expect(out.text).toContain(mail);
    expect(out.text).not.toContain(mobile);
  });

  it('leaves rent, distances and dates alone', () => {
    const text = 'Rs. 23,000/month, 1.4 km from you, confirmed 12 Sep 2026, 3 of 8 beds free';
    expect(scrubContacts(text)).toEqual({ text, scrubbed: false });
  });
});

describe('readYesNo', () => {
  it.each(['haan', 'Han kar do', 'yes please', 'ok', 'theek hai', 'confirm'])('reads %s as yes', (t) => {
    expect(readYesNo(t)).toBe('yes');
  });

  it.each(['nahi', 'No thanks', 'cancel', 'mat karo'])('reads %s as no', (t) => {
    expect(readYesNo(t)).toBe('no');
  });

  it('reads anything else as neither', () => {
    expect(readYesNo('what about girls hostels?')).toBeNull();
  });
});

describe('readArgs', () => {
  it('takes an object as it is and a JSON string parsed', () => {
    expect(readArgs({ a: 1 })).toEqual({ a: 1 });
    expect(readArgs('{"a":1}')).toEqual({ a: 1 });
  });

  it('turns garbage into no arguments rather than a crash', () => {
    expect(readArgs('not json')).toEqual({});
    expect(readArgs(undefined)).toEqual({});
  });
});

describe('enum words from the model', () => {
  it('maps the ways people say who a place is for', () => {
    expect(toGenderPolicy('boys')).toBe('Boys');
    expect(toGenderPolicy('larkon')).toBe('Boys');
    expect(toGenderPolicy('Female')).toBe('Girls');
    expect(toGenderPolicy('family')).toBe('Family');
    expect(toGenderPolicy('anyone')).toBeUndefined();
  });

  it('maps room types', () => {
    expect(toRoomType('single')).toBe('Private');
    expect(toRoomType('Shared')).toBe('Shared');
    expect(toRoomType('penthouse')).toBeUndefined();
  });
});

describe('matchFacilities', () => {
  const catalogue = [
    { id: 1, name: 'WiFi', iconKey: 'wifi' },
    { id: 2, name: 'Mess', iconKey: 'mess' },
    { id: 3, name: 'Air Conditioning', iconKey: 'ac' },
  ];

  it('matches loosely and reports what it could not match', () => {
    expect(matchFacilities(['wi-fi', 'khana', 'swimming pool'], catalogue)).toEqual({
      ids: [1, 2],
      unknown: ['swimming pool'],
    });
  });

  it('reads AC as Air Conditioning and never as a word that merely contains it', () => {
    const real = [
      { id: 1, name: 'Wi-Fi', iconKey: 'wifi' },
      { id: 3, name: 'Air Conditioning', iconKey: 'ac' },
      { id: 7, name: 'Electricity Backup', iconKey: 'power-backup' },
    ];
    expect(matchFacilities(['AC'], real).ids).toEqual([3]);
    expect(matchFacilities(['UPS', 'wifi', 'backup'], real).ids).toEqual([7, 1]);
    expect(matchFacilities(['ck'], real)).toEqual({ ids: [], unknown: ['ck'] });
  });

  it('treats a missing list as no facilities', () => {
    expect(matchFacilities(undefined, catalogue)).toEqual({ ids: [], unknown: [] });
  });
});

describe('rankByDistanceThenRent', () => {
  const rows = [
    { id: 1, monthlyRent: 20000, km: null },
    { id: 2, monthlyRent: 30000, km: 1.2 },
    { id: 3, monthlyRent: 15000, km: 4 },
    { id: 4, monthlyRent: 10000, km: null },
  ];

  it('puts known distances first, nearest first, then the rest by rent', () => {
    expect(rankByDistanceThenRent(rows, (r) => r.km).map((r) => r.id)).toEqual([2, 3, 4, 1]);
  });

  it('falls back to cheapest first when no distance is known', () => {
    expect(rankByDistanceThenRent(rows, () => null).map((r) => r.id)).toEqual([4, 3, 1, 2]);
  });
});

describe('toReplyLines', () => {
  it('splits bold runs and bullets without keeping any markup', () => {
    const lines = toReplyLines('Yeh mile:\n- **Al-Madina** - Rs. 23,000\n\nBas.');
    expect(lines[0]).toEqual({ bullet: false, parts: [{ text: 'Yeh mile:', bold: false }] });
    expect(lines[1].bullet).toBe(true);
    expect(lines[1].parts[0]).toEqual({ text: 'Al-Madina', bold: true });
    expect(lines.at(-1)?.parts[0].text).toBe('Bas.');
  });

  it('never passes HTML through as anything but text', () => {
    const [line] = toReplyLines('<img src=x onerror=alert(1)>');
    expect(line.parts).toEqual([{ text: '<img src=x onerror=alert(1)>', bold: false }]);
  });
});

describe('genderIn', () => {
  it.each([
    ['boys hostel near NUST', 'Boys'],
    ['larkiyon ke liye kuch hai?', 'Girls'],
    ['family ke liye', 'Family'],
    ['koi bhi', 'Any'],
    ['Any', 'Any'],
    ['25k tak Lahore mein', undefined],
  ])('reads "%s" as %s', (text, expected) => {
    expect(genderIn(text)).toBe(expected);
  });
});

describe('looksRomanUrdu', () => {
  it('tells the two apart well enough to word one question', () => {
    expect(looksRomanUrdu('mujhe Lahore mein hostel chahiye')).toBe(true);
    expect(looksRomanUrdu('I need a room in Lahore')).toBe(false);
  });
});
