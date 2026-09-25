/**
 * Shapes for the assistant: what goes to the model, what comes back, and what the chat
 * panel draws. The model's own wire format is Ollama's `/api/chat`; the panel's entries are
 * ours, because a reply is more than text - it can carry hostel cards, a button, or a
 * question the person has to answer before anything is written.
 */

export type LlmRole = 'system' | 'user' | 'assistant' | 'tool';

export interface LlmToolCall {
  function: { name: string; arguments: Record<string, unknown> | string };
}

export interface LlmMessage {
  role: LlmRole;
  content: string;
  tool_calls?: LlmToolCall[];
  /** Set on a `tool` message: which tool this is the answer from. */
  tool_name?: string;
}

/** A tool as the model is told about it - JSON Schema parameters, Ollama/OpenAI style. */
export interface ToolDefinition {
  type: 'function';
  function: {
    name: string;
    description: string;
    parameters: {
      type: 'object';
      properties: Record<string, unknown>;
      required?: string[];
    };
  };
}

/** A listing as the panel draws it: the card, plus the two things worked out for it. */
export interface HostelCard {
  id: number;
  title: string;
  location: string;
  monthlyRent: number;
  genderPolicy: string;
  roomType: string;
  availableBeds: number;
  totalBeds: number;
  thumbnailUrl: string | null;
  inspected: boolean;
  /** Already worded - "1.4 km from you (straight-line)" - or null when unknown. */
  distanceLabel: string | null;
  averageRating: number | null;
}

/** A link the panel offers under a reply: "Open comparison", "Sign in", "Verify email". */
export interface AgentLink {
  label: string;
  commands: unknown[];
  queryParams?: Record<string, unknown>;
}

/**
 * What running one tool produced.
 *
 * `result` is what the model reads next, and nothing else from here reaches it. `cards` and
 * `links` go only to the panel - the model never sees them, so it cannot garble them either.
 */
export interface ToolOutcome {
  result: unknown;
  cards?: HostelCard[];
  links?: AgentLink[];
  /**
   * Set when the tool needs one thing from the person before it can run. The loop asks it
   * in fixed words and stops, rather than trusting the model to ask.
   */
  ask?: 'gender';
}

/** What the loop knows about the conversation that a single tool call cannot see. */
export interface ToolContext {
  /** The "who is it for" question has already been asked in this conversation. */
  genderAsked?: boolean;
  /** Who it is for, as the person last said it in their own words, if they did. */
  genderHint?: 'Boys' | 'Girls' | 'Family' | 'Any';
}

/**
 * A tool call the person must agree to before it runs. Built by the toolbox rather than by
 * the model, so the sentence the person agrees to describes what will actually be sent.
 */
export interface PendingAction {
  summary: string;
  run: () => Promise<ToolOutcome>;
}

export type ChatEntry =
  | { kind: 'user'; text: string }
  | { kind: 'assistant'; text: string }
  | { kind: 'cards'; cards: HostelCard[] }
  | { kind: 'links'; links: AgentLink[] }
  | { kind: 'confirm'; id: number; summary: string; state: 'waiting' | 'done' | 'declined' }
  | { kind: 'choices'; options: string[] }
  | { kind: 'error'; text: string };
