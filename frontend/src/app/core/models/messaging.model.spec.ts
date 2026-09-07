import { describe, expect, it } from 'vitest';
import {
  Conversation,
  MESSAGE_MAX,
  Message,
  bumpConversation,
  markRead,
  messageProblem,
  sentLabel,
  totalUnread,
  upsertMessage,
} from './messaging.model';

function message(over: Partial<Message> = {}): Message {
  return {
    id: 1,
    conversationId: 4,
    senderDisplayName: 'Kashif Mehmood',
    isMine: false,
    body: 'Is the room still free?',
    sentAt: '2026-09-07T10:00:00',
    readAt: null,
    ...over,
  };
}

function conversation(over: Partial<Conversation> = {}): Conversation {
  return {
    id: 4,
    propertyId: 4,
    propertyTitle: 'Single room with sea breeze, Clifton',
    otherParticipantDisplayName: 'Kashif Mehmood',
    createdAt: '2026-09-06T10:00:00',
    lastMessageAt: '2026-09-06T11:00:00',
    unreadCount: 0,
    ...over,
  };
}

describe('messageProblem', () => {
  it('refuses an empty body and one that is only spaces', () => {
    expect(messageProblem('')).not.toBeNull();
    expect(messageProblem('   ')).not.toBeNull();
  });

  it('allows a normal message', () => {
    expect(messageProblem('Assalam o alaikum')).toBeNull();
  });

  it('allows exactly the maximum and refuses one past it', () => {
    expect(messageProblem('x'.repeat(MESSAGE_MAX))).toBeNull();
    expect(messageProblem('x'.repeat(MESSAGE_MAX + 1))).toContain('too long');
  });
});

describe('upsertMessage', () => {
  it('adds a message the thread has not seen', () => {
    expect(upsertMessage([], message({ id: 7 }))).toHaveLength(1);
  });

  /**
   * The whole reason this function exists. The hub pushes a message and the poll fetches
   * the same one seconds later; appending both draws the bubble twice.
   */
  it('does not draw the same message twice when it arrives on both channels', () => {
    const pushed = message({ id: 7, body: 'from the hub' });
    const polled = message({ id: 7, body: 'from the poll' });

    const thread = upsertMessage(upsertMessage([], pushed), polled);

    expect(thread).toHaveLength(1);
    expect(thread[0].body).toBe('from the poll');
  });

  it('keeps the thread in sent order when a push arrives out of order', () => {
    const later = message({ id: 9, sentAt: '2026-09-07T12:00:00' });
    const earlier = message({ id: 8, sentAt: '2026-09-07T11:00:00' });

    const thread = upsertMessage(upsertMessage([], later), earlier);

    expect(thread.map((m) => m.id)).toEqual([8, 9]);
  });
});

describe('bumpConversation', () => {
  it('moves the thread to the top and counts it unread', () => {
    const list = [conversation({ id: 1 }), conversation({ id: 4 })];

    const after = bumpConversation(list, message({ conversationId: 4 }), null);

    expect(after[0].id).toBe(4);
    expect(after[0].unreadCount).toBe(1);
  });

  it('does not count a message unread while that thread is on screen', () => {
    const list = [conversation({ id: 4 })];

    const after = bumpConversation(list, message({ conversationId: 4 }), 4);

    expect(after[0].unreadCount).toBe(0);
  });

  it('does not count your own message unread', () => {
    const list = [conversation({ id: 4 })];

    const after = bumpConversation(list, message({ conversationId: 4, isMine: true }), null);

    expect(after[0].unreadCount).toBe(0);
  });

  it('leaves the list alone for a thread it has never seen', () => {
    const list = [conversation({ id: 1 })];

    expect(bumpConversation(list, message({ conversationId: 99 }), null)).toBe(list);
  });
});

describe('the unread badge', () => {
  it('adds up across threads', () => {
    expect(
      totalUnread([conversation({ id: 1, unreadCount: 2 }), conversation({ id: 4, unreadCount: 3 })]),
    ).toBe(5);
  });

  it('clears on the thread that was read and no other', () => {
    const after = markRead(
      [conversation({ id: 1, unreadCount: 2 }), conversation({ id: 4, unreadCount: 3 })],
      4,
    );

    expect(after[0].unreadCount).toBe(2);
    expect(after[1].unreadCount).toBe(0);
  });
});

describe('sentLabel', () => {
  const now = new Date('2026-09-07T18:00:00');

  it('gives a time for today and names yesterday', () => {
    expect(sentLabel('2026-09-07T09:30:00', now)).not.toContain(',');
    expect(sentLabel('2026-09-06T09:30:00', now)).toContain('Yesterday');
  });

  it('gives a weekday within the week and a date beyond it', () => {
    expect(sentLabel('2026-09-03T09:30:00', now)).toMatch(/day,/);
    expect(sentLabel('2026-08-01T09:30:00', now)).toMatch(/Aug/);
  });

  it('says nothing rather than NaN when the stamp is unreadable', () => {
    expect(sentLabel('not a date', now)).toBe('');
  });
});
