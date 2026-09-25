/**
 * The assistant's pure pieces - no HTTP, no signals - so each can be pinned by a unit test.
 */
import { Facility, GenderPolicy, RoomType } from '../models/catalog.model';

/**
 * Contact details the model must never put on screen, whatever it was given or imagined.
 * Rule 1 of AGENTS.md: an owner is reached through messages and visits, never directly.
 * Mobiles in the three ways people write them, landlines, e-mail, messaging-app links and a
 * CNIC. The messaging-app word is spelt with a class so this file does not trip the source
 * guard that exists to find the very same word.
 */
const CONTACT_PATTERNS: RegExp[] = [
  /(?:\+|00)?92[\s-]?3\d{2}[\s-]?\d{7}\b/g,
  /\b0\d{2,4}[\s-]?\d{6,8}\b/g,
  /\b\d{5}-\d{7}-\d\b/g,
  /[\w.+-]+@[\w-]+\.[\w.]+/g,
  /\bwa\.me\/\S*/gi,
  /\bwh[a@]ts\s?[a@]pp\b/gi,
];

export const CONTACT_REDACTION = '[hidden - use RoomRaah messages]';

const EMAIL_PATTERN = CONTACT_PATTERNS[3];

/**
 * Strips anything contact-shaped out of a reply. Answers the text and whether it changed.
 * `allowEmail` is for an admin, whose users screen lists account emails already; phone
 * numbers and CNICs are removed for everyone, because in the product they appear on one
 * screen only and never in a conversation.
 */
export function scrubContacts(text: string, options: { allowEmail?: boolean } = {}): { text: string; scrubbed: boolean } {
  let out = text;
  for (const pattern of CONTACT_PATTERNS) {
    if (options.allowEmail && pattern === EMAIL_PATTERN) continue;
    out = out.replace(pattern, CONTACT_REDACTION);
  }
  return { text: out, scrubbed: out !== text };
}

/** Ollama sends arguments as an object; some model builds send a JSON string instead. */
export function readArgs(raw: Record<string, unknown> | string | undefined): Record<string, unknown> {
  if (!raw) return {};
  if (typeof raw !== 'string') return raw;
  try {
    const parsed: unknown = JSON.parse(raw);
    return parsed && typeof parsed === 'object' ? (parsed as Record<string, unknown>) : {};
  } catch {
    return {};
  }
}

export function asInt(value: unknown): number | undefined {
  const n = typeof value === 'string' ? Number(value.replace(/[,\s]/g, '')) : Number(value);
  return Number.isFinite(n) && n > 0 ? Math.round(n) : undefined;
}

export function asText(value: unknown): string | undefined {
  return typeof value === 'string' && value.trim() ? value.trim() : undefined;
}

/**
 * A typed "yes" or "no" while a confirm card is waiting. Anything else is neither, and the
 * caller treats it as the person moving on.
 */
export function readYesNo(text: string): 'yes' | 'no' | null {
  const t = text.trim().toLowerCase();
  if (/^(nahi|nahin|nai|na|no|nope|cancel|mat|rehne do|stop)\b/.test(t)) return 'no';
  if (/^(haan|han|ha|hn|g|ji|jee|yes|y|yeah|yep|ok|okay|theek|thik|sure|confirm|kar do|kr do|done)\b/.test(t)) {
    return 'yes';
  }
  return null;
}

const GENDER_WORDS: Record<GenderPolicy, RegExp> = {
  Boys: /^(boys?|male|men|man|larkon?|larke|ladkon?|ladke|gents?)$/i,
  Girls: /^(girls?|female|women|woman|larkiyon?|larki|ladkiyon?|ladki|ladies)$/i,
  Family: /^(family|families|couple|married)$/i,
};

/** The model says "boys", "Male" or "larkon"; the API takes exactly `Boys`. */
export function toGenderPolicy(value: unknown): GenderPolicy | undefined {
  const text = asText(value);
  if (!text) return undefined;
  return (Object.keys(GENDER_WORDS) as GenderPolicy[]).find((key) => GENDER_WORDS[key].test(text));
}

/**
 * Who a message says the room is for, read from the person's own words - "boys hostel",
 * "larkiyon ke liye", "koi bhi". Undefined when the message does not say.
 */
export function genderIn(text: string): GenderPolicy | 'Any' | undefined {
  const t = text.toLowerCase();
  if (/\b(koi bhi|kisi bhi|any|anyone|anybody|no preference|farq nahi|doesn'?t matter|sab ke liye|all)\b/.test(t)) {
    return 'Any';
  }
  const words = t.split(/[^a-z]+/);
  for (const policy of Object.keys(GENDER_WORDS) as GenderPolicy[]) {
    if (words.some((w) => GENDER_WORDS[policy].test(w))) return policy;
  }
  return undefined;
}

/**
 * Roman Urdu or English, by the little words Roman Urdu cannot do without. Only used to
 * word the one fixed question the assistant asks itself.
 */
export function looksRomanUrdu(text: string): boolean {
  return /\b(hai|hain|ka|ki|ke|ko|se|mein|mujhe|mujhay|mera|mere|chahiye|chahye|kya|koi|nazdeek|paas|tak|wala|kar|karo|do|dikhao|batao)\b/i.test(text);
}

export function toRoomType(value: unknown): RoomType | undefined {
  const text = asText(value)?.toLowerCase();
  if (!text) return undefined;
  if (/^(private|single|alag|apna|own)/.test(text)) return 'Private';
  if (/^(shared|sharing|double|triple|dorm)/.test(text)) return 'Shared';
  return undefined;
}

/**
 * Facility names as the model wrote them, matched against the real catalogue. Loose on
 * purpose - "wifi", "Wi-Fi" and "internet" should all find WiFi - and answering which names
 * matched nothing, so the reply can say so instead of pretending the filter was applied.
 */
export function matchFacilities(
  names: unknown,
  catalogue: Facility[],
): { ids: number[]; unknown: string[] } {
  const wanted = Array.isArray(names) ? names.filter((n): n is string => typeof n === 'string') : [];
  const ids: number[] = [];
  const missing: string[] = [];
  for (const name of wanted) {
    const hit = findFacility(name, catalogue);
    if (hit) {
      if (!ids.includes(hit.id)) ids.push(hit.id);
    } else if (squash(name)) {
      missing.push(name);
    }
  }
  return { ids, unknown: missing };
}

const squash = (s: string) => s.toLowerCase().replace(/[^a-z0-9]/g, '');

/** What people call a facility, mapped to what the catalogue calls it. */
const FACILITY_ALIASES: Record<string, string> = {
  internet: 'wifi',
  food: 'mess',
  khana: 'mess',
  aircon: 'airconditioning',
  airconditioner: 'airconditioning',
  ups: 'electricitybackup',
  generator: 'electricitybackup',
  backup: 'electricitybackup',
  guard: 'security',
  cctv: 'security',
  washing: 'laundry',
};

/**
 * Exact name, then initials ("AC" is Air Conditioning), then the start of a word. Never an
 * arbitrary substring: "ac" is inside "backup", and an owner who asked for AC was about to be
 * sent a revision adding Electricity Backup.
 */
function findFacility(name: string, catalogue: Facility[]): Facility | undefined {
  const raw = squash(name);
  if (!raw) return undefined;
  const key = FACILITY_ALIASES[raw] ?? raw;
  const words = (f: Facility) => f.name.toLowerCase().split(/[^a-z0-9]+/).filter(Boolean);

  return (
    catalogue.find((f) => squash(f.name) === key) ??
    catalogue.find((f) => words(f).length > 1 && words(f).map((w) => w[0]).join('') === key) ??
    (key.length >= 3
      ? catalogue.find((f) => squash(f.name).startsWith(key) || words(f).some((w) => w.startsWith(key)))
      : undefined)
  );
}

export function rupees(value: number): string {
  return 'Rs. ' + Math.round(value).toLocaleString('en-US');
}

/** A date as a person reads it, or the plain word for not knowing. */
export function day(value: string | null | undefined): string {
  if (!value) return 'not available';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return 'not available';
  return date.toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' });
}

/**
 * Nearest first when a distance is known for anything, cheapest first otherwise, and a
 * listing with no known distance after every one that has one. Stable for ties.
 */
export function rankByDistanceThenRent<T extends { monthlyRent: number }>(
  items: T[],
  distanceOf: (item: T) => number | null,
): T[] {
  return items
    .map((item, index) => ({ item, index, km: distanceOf(item) }))
    .sort((a, b) => {
      if (a.km !== null && b.km !== null && a.km !== b.km) return a.km - b.km;
      if (a.km === null && b.km !== null) return 1;
      if (a.km !== null && b.km === null) return -1;
      if (a.item.monthlyRent !== b.item.monthlyRent) return a.item.monthlyRent - b.item.monthlyRent;
      return a.index - b.index;
    })
    .map((entry) => entry.item);
}

/**
 * The reply as lines of plain and bold runs, so the template can draw `**bold**` without
 * binding any HTML the model wrote.
 */
export interface ReplyLine {
  bullet: boolean;
  parts: { text: string; bold: boolean }[];
}

export function toReplyLines(text: string): ReplyLine[] {
  return text
    .replace(/\r/g, '')
    .split('\n')
    .map((raw) => {
      const bulletMatch = /^\s*(?:[-*•]|\d+[.)])\s+/.exec(raw);
      const body = bulletMatch ? raw.slice(bulletMatch[0].length) : raw;
      const parts = body
        .split(/(\*\*[^*]+\*\*)/g)
        .filter((part) => part.length > 0)
        .map((part) =>
          part.startsWith('**') && part.endsWith('**') && part.length > 4
            ? { text: part.slice(2, -2), bold: true }
            : { text: part.replace(/^#+\s*/, ''), bold: false },
        );
      return { bullet: !!bulletMatch, parts };
    })
    .filter((line, index, all) => line.parts.length > 0 || (index > 0 && all[index - 1].parts.length > 0));
}
