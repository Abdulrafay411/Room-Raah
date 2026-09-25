import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { AgentToolbox, Prepared } from './agent-tools';
import { LlmMessage } from './agent.model';
import { AgentService, GENDER_QUESTION, NOT_DONE, OFFLINE_MESSAGE, trimHistory } from './agent.service';
import { OllamaClient } from './ollama.client';

/** A model that answers from a script, one reply per call, and records what it was sent. */
function scriptedModel(replies: LlmMessage[]) {
  const sent: LlmMessage[][] = [];
  return {
    sent,
    chat: vi.fn((messages: LlmMessage[], _tools?: unknown[]): Observable<LlmMessage> => {
      sent.push(structuredClone(messages));
      const next = replies.shift();
      return next ? of(next) : throwError(() => new Error('script ran out'));
    }),
    warmUp: vi.fn(() => of(true)),
  };
}

const call = (name: string, args: Record<string, unknown>): LlmMessage => ({
  role: 'assistant',
  content: '',
  tool_calls: [{ function: { name, arguments: args } }],
});
const say = (content: string): LlmMessage => ({ role: 'assistant', content });

describe('AgentService', () => {
  let toolbox: { definitions: ReturnType<typeof vi.fn>; prepare: ReturnType<typeof vi.fn> };

  function setup(model: ReturnType<typeof scriptedModel>): AgentService {
    TestBed.configureTestingModule({
      providers: [
        { provide: OllamaClient, useValue: model },
        { provide: AgentToolbox, useValue: toolbox },
        { provide: Router, useValue: { url: '/search', navigate: vi.fn() } },
        {
          provide: AuthService,
          useValue: {
            currentUser: () => null,
            isAuthenticated: () => false,
            isEmailVerified: () => false,
            userRole: () => null,
          },
        },
      ],
    });
    return TestBed.inject(AgentService);
  }

  beforeEach(() => {
    sessionStorage.clear();
    toolbox = { definitions: vi.fn(() => []), prepare: vi.fn() };
  });

  it('runs a tool the model asks for, shows its cards, and ends on the reply', async () => {
    const model = scriptedModel([call('search_hostels', { maxRent: 25000 }), say('Yeh 1 hostel mila.')]);
    const card = { id: 12, title: 'Al-Madina' } as never;
    toolbox.prepare.mockResolvedValue({ outcome: { result: { hostels: [{ id: 12 }] }, cards: [card] } } as Prepared);
    const agent = setup(model);

    await agent.send('25k tak hostel');

    expect(toolbox.prepare).toHaveBeenCalledWith('search_hostels', { maxRent: 25000 }, expect.anything());
    expect(agent.entries().map((e) => e.kind)).toEqual(['user', 'cards', 'assistant']);
    // The second call to the model carried the tool's answer.
    const second = model.sent[1];
    expect(second.at(-1)).toEqual({ role: 'tool', tool_name: 'search_hostels', content: '{"hostels":[{"id":12}]}' });
    expect(second[0].role).toBe('system');
    expect(agent.busy()).toBe(false);
  });

  it('asks who the room is for in fixed words, with buttons, then searches with the answer', async () => {
    const model = scriptedModel([
      call('search_hostels', { place: 'Lahore' }),
      call('search_hostels', { place: 'Lahore', genderPolicy: 'Girls' }),
      say('Yeh mile.'),
    ]);
    toolbox.prepare
      .mockResolvedValueOnce({ outcome: { result: { askFirst: 'gender' }, ask: 'gender' } } as Prepared)
      .mockResolvedValueOnce({ outcome: { result: { hostels: [] } } } as Prepared);
    const agent = setup(model);

    await agent.send('Lahore mein hostel chahiye');
    expect(model.chat).toHaveBeenCalledOnce();
    expect(agent.entries().slice(-2)).toEqual([
      { kind: 'assistant', text: GENDER_QUESTION.urdu },
      { kind: 'choices', options: GENDER_QUESTION.options },
    ]);

    await agent.send('Girls');
    expect(toolbox.prepare).toHaveBeenLastCalledWith(
      'search_hostels',
      { place: 'Lahore', genderPolicy: 'Girls' },
      { genderAsked: true, genderHint: 'Girls' },
    );
  });

  it('stops for the person before anything is written, and runs it only on confirm', async () => {
    const model = scriptedModel([call('save_hostel', { propertyId: 12 }), say('Save ho gaya.')]);
    const run = vi.fn().mockResolvedValue({ result: { ok: true } });
    toolbox.prepare.mockResolvedValue({ pending: { summary: 'Save "Al-Madina"?', run } } as Prepared);
    const agent = setup(model);

    const done = agent.send('save kar do');
    await vi.waitFor(() => expect(agent.entries().some((e) => e.kind === 'confirm')).toBe(true));
    expect(run).not.toHaveBeenCalled();

    const confirm = agent.entries().find((e) => e.kind === 'confirm');
    agent.confirm((confirm as { id: number }).id, true);
    await done;

    expect(run).toHaveBeenCalledOnce();
    expect(agent.entries().find((e) => e.kind === 'confirm')).toMatchObject({ state: 'done' });
    expect(agent.entries().at(-1)).toEqual({ kind: 'assistant', text: 'Save ho gaya.' });
  });

  it('treats a typed "nahi" as a no and says so itself, without asking the model', async () => {
    const model = scriptedModel([call('save_hostel', { propertyId: 12 }), say('Save kar diya!')]);
    const run = vi.fn();
    toolbox.prepare.mockResolvedValue({ pending: { summary: 'Save?', run } } as Prepared);
    const agent = setup(model);

    const done = agent.send('save kar do');
    await vi.waitFor(() => expect(agent.entries().some((e) => e.kind === 'confirm')).toBe(true));
    await agent.send('nahi');
    await done;

    expect(run).not.toHaveBeenCalled();
    // The model never got the chance to claim the save happened.
    expect(model.chat).toHaveBeenCalledOnce();
    expect(agent.entries().at(-1)).toEqual({ kind: 'assistant', text: NOT_DONE });
  });

  it('answers what was typed instead of a yes, as a turn of its own', async () => {
    const model = scriptedModel([call('save_hostel', { propertyId: 12 }), say('Girls hostels yeh hain.')]);
    toolbox.prepare.mockResolvedValue({ pending: { summary: 'Save?', run: vi.fn() } } as Prepared);
    const agent = setup(model);

    const done = agent.send('save kar do');
    await vi.waitFor(() => expect(agent.entries().some((e) => e.kind === 'confirm')).toBe(true));
    await agent.send('girls hostel dikhao');
    await done;

    expect(model.sent[1].at(-1)).toEqual({ role: 'user', content: 'girls hostel dikhao' });
    expect(agent.entries().map((e) => e.kind)).toEqual(['user', 'confirm', 'user', 'assistant', 'assistant']);
  });

  it('never shows a phone number the model made up', async () => {
    const leak = ['0300', '1234567'].join('-');
    const agent = setup(scriptedModel([say(`Owner ko ${leak} par call karein`)]));
    await agent.send('number do');
    const reply = agent.entries().at(-1) as { text: string };
    expect(reply.text).not.toContain(leak);
  });

  it('says the assistant is offline when the model cannot be reached', async () => {
    const agent = setup(scriptedModel([]));
    await agent.send('hello');
    expect(agent.entries().at(-1)).toEqual({ kind: 'error', text: OFFLINE_MESSAGE });
    expect(agent.online()).toBe(false);
  });

  it('keeps the conversation across a reload of the tab', async () => {
    const first = setup(scriptedModel([say('Salam!')]));
    await first.send('salam');
    TestBed.resetTestingModule();

    const second = setup(scriptedModel([]));
    expect(second.entries().map((e) => e.kind)).toEqual(['user', 'assistant']);
  });
});

describe('trimHistory', () => {
  it('never starts the kept window on an orphaned tool answer', () => {
    const history: LlmMessage[] = [
      { role: 'user', content: 'a' },
      { role: 'assistant', content: '', tool_calls: [] },
      { role: 'tool', content: '{}' },
      { role: 'assistant', content: 'x' },
      { role: 'user', content: 'b' },
      { role: 'assistant', content: 'y' },
    ];
    expect(trimHistory(history, 4)).toEqual(history.slice(4));
  });
});

describe('AgentService, when the model repeats itself', () => {
  it('answers a repeated call from memory, shows the cards once, and makes the model answer', async () => {
    const toolbox = {
      definitions: vi.fn(() => [{ type: 'function', function: { name: 'search_hostels' } }]),
      prepare: vi.fn().mockResolvedValue({ outcome: { result: { hostels: [] }, cards: [{ id: 7 }] } }),
    };
    const search = call('search_hostels', { place: 'Islamabad', genderPolicy: 'Boys' });
    const same = call('search_hostels', { genderPolicy: 'Boys', place: 'Islamabad' });
    const model = scriptedModel([search, same, say('Islamabad mein boys ke liye koi hostel nahi.')]);
    TestBed.configureTestingModule({
      providers: [
        { provide: OllamaClient, useValue: model },
        { provide: AgentToolbox, useValue: toolbox },
        { provide: Router, useValue: { url: '/search', navigate: vi.fn() } },
        { provide: AuthService, useValue: { currentUser: () => null, isAuthenticated: () => false, isEmailVerified: () => false, userRole: () => null } },
      ],
    });
    sessionStorage.clear();
    const agent = TestBed.inject(AgentService);

    await agent.send('boys hostel near FAST islamabaad');

    expect(toolbox.prepare).toHaveBeenCalledOnce();
    expect(agent.entries().filter((e) => e.kind === 'cards')).toHaveLength(1);
    // The third call offered no tools, so the model had to answer.
    expect(model.chat.mock.calls[2][1]).toEqual([]);
    expect(agent.entries().at(-1)).toEqual({ kind: 'assistant', text: 'Islamabad mein boys ke liye koi hostel nahi.' });
  });

  it('keeps "let me check" chatter between tool calls off the screen', async () => {
    const toolbox = { definitions: vi.fn(() => []), prepare: vi.fn().mockResolvedValue({ outcome: { result: {} } }) };
    const model = scriptedModel([{ ...call('list_cities', {}), content: 'Let me check...' }, say('Teen cities hain.')]);
    TestBed.configureTestingModule({
      providers: [
        { provide: OllamaClient, useValue: model },
        { provide: AgentToolbox, useValue: toolbox },
        { provide: Router, useValue: { url: '/', navigate: vi.fn() } },
        { provide: AuthService, useValue: { currentUser: () => null, isAuthenticated: () => false, isEmailVerified: () => false, userRole: () => null } },
      ],
    });
    sessionStorage.clear();
    const agent = TestBed.inject(AgentService);
    await agent.send('kitni cities?');
    expect(agent.entries().filter((e) => e.kind === 'assistant')).toEqual([{ kind: 'assistant', text: 'Teen cities hain.' }]);
  });
});

describe('AgentService, across accounts', () => {
  it('drops the conversation when a different person signs in', async () => {
    sessionStorage.clear();
    const user = signal<{ id: number } | null>({ id: 6 });
    TestBed.configureTestingModule({
      providers: [
        { provide: OllamaClient, useValue: scriptedModel([say('Salam!')]) },
        { provide: AgentToolbox, useValue: { definitions: () => [], prepare: vi.fn() } },
        { provide: Router, useValue: { url: '/', navigate: vi.fn() } },
        {
          provide: AuthService,
          useValue: { currentUser: user, isAuthenticated: () => true, isEmailVerified: () => true, userRole: () => 'Seeker' },
        },
      ],
    });
    const agent = TestBed.inject(AgentService);
    TestBed.tick();
    await agent.send('salam');
    expect(agent.entries()).toHaveLength(2);

    user.set({ id: 9 });
    TestBed.tick();
    expect(agent.entries()).toEqual([]);
  });
});
