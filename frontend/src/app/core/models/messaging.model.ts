/**
 * Messaging, from the brief's section 8.8 and section 9.
 *
 * Two things about this feature shape everything below. First, a seeker opens a
 * conversation and an owner replies - there is no directory of people to start one from,
 * because contact in this product happens against a listing or not at all. Second, the
 * same message arrives twice: once over the hub and once from the next poll or reload.
 * Nothing here may assume it has seen a message for the first time.
 */

/** Brief section 7: `body` is required, 1 to 2000. */
export const MESSAGE_MAX = 2000;

/** How often the fallback poll runs when the hub is not connected. Brief section 9. */
export const POLL_MS = 10_000;

export interface Conversation {
  id: number;
  propertyId: number;
  propertyTitle: string;
  /** Rule 93 again: a display name, and nothing to reach them with off the site. */
  otherParticipantDisplayName: string;
  createdAt: string;
  lastMessageAt: string;
  unreadCount: number;
}

export interface Message {
  id: number;
  conversationId: number;
  senderDisplayName: string;
  /**
   * The only thing saying which side to draw a bubble on. There is no sender id in this
   * shape, on purpose, so this flag is the whole of it.
   */
  isMine: boolean;
  body: string;
  sentAt: string;
  readAt: string | null;
}

export interface ConversationCreated {
  id: number;
}

export interface ReadReceipt {
  conversationId: number;
  /** Zero is a success, not a refusal - it means there was nothing left to mark. */
  markedCount: number;
}

/** Said before the send button is pressed rather than after a 400. */
export function messageProblem(body: string): string | null {
  const text = body.trim();
  if (!text) return 'Write something first.';
  if (text.length > MESSAGE_MAX) {
    return `That is ${text.length - MESSAGE_MAX} characters too long — messages stop at ${MESSAGE_MAX}.`;
  }
  return null;
}

/**
 * Adds a message to a thread, or replaces it if the thread already holds it.
 *
 * This exists because of the one bug this feature is guaranteed to have otherwise. The hub
 * pushes a message the moment it is written, and the poll and the next thread load fetch
 * the same message again a few seconds later. Appending blindly draws it twice, and a
 * demo with doubled bubbles reads as broken even though nothing is. Identity is the id.
 *
 * The result is sorted by `sentAt`, because a pushed message can land before an older one
 * that a poll was already fetching.
 */
export function upsertMessage(thread: Message[], incoming: Message): Message[] {
  const seen = thread.some((message) => message.id === incoming.id);
  const merged = seen
    ? thread.map((message) => (message.id === incoming.id ? incoming : message))
    : [...thread, incoming];

  return merged.sort((a, b) => new Date(a.sentAt).getTime() - new Date(b.sentAt).getTime());
}

/**
 * Folds an arriving message into the conversation list: its thread goes to the top, its
 * stamp moves, and it counts as unread unless the person is looking at that thread now.
 *
 * A message for a conversation the list has never seen - the first reply on a thread
 * opened in another tab - leaves the list alone. The caller reloads for that case rather
 * than inventing a row out of a message, which does not carry enough to build one.
 */
export function bumpConversation(
  conversations: Conversation[],
  message: Message,
  activeId: number | null,
): Conversation[] {
  const index = conversations.findIndex((c) => c.id === message.conversationId);
  if (index < 0) return conversations;

  const reading = activeId === message.conversationId;
  const updated: Conversation = {
    ...conversations[index],
    lastMessageAt: message.sentAt,
    unreadCount: reading || message.isMine ? 0 : conversations[index].unreadCount + 1,
  };

  const rest = conversations.filter((_, i) => i !== index);
  return [updated, ...rest];
}

export function totalUnread(conversations: Conversation[]): number {
  return conversations.reduce((sum, c) => sum + c.unreadCount, 0);
}

/** Clears the badge on one row, for when a thread has just been read. */
export function markRead(conversations: Conversation[], id: number): Conversation[] {
  return conversations.map((c) => (c.id === id ? { ...c, unreadCount: 0 } : c));
}

/**
 * The stamp above a bubble. Same day gives a time, this week gives a weekday, older gives
 * a date - a wall of identical full timestamps is unreadable in a thread.
 */
export function sentLabel(sentAt: string, now: Date = new Date()): string {
  const when = new Date(sentAt);
  if (Number.isNaN(when.getTime())) return '';

  const time = when.toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' });
  const days = Math.floor((startOfDay(now) - startOfDay(when)) / 86_400_000);

  if (days === 0) return time;
  if (days === 1) return `Yesterday, ${time}`;
  if (days < 7) return `${when.toLocaleDateString(undefined, { weekday: 'long' })}, ${time}`;
  return `${when.toLocaleDateString(undefined, { day: 'numeric', month: 'short' })}, ${time}`;
}

function startOfDay(date: Date): number {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate()).getTime();
}
