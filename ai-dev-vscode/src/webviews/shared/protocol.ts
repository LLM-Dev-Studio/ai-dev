import type { AgentSummary, MessageItem, DecisionItem } from '../../types';

// Extension host → webview
export type ToAgentsWebview =
  | { type: 'loading' }
  | { type: 'error'; message: string }
  | { type: 'agents'; data: AgentSummary[] };

export type ToMessagesWebview =
  | { type: 'loading' }
  | { type: 'error'; message: string }
  | { type: 'messages'; data: MessageItem[] };

export type ToDecisionsWebview =
  | { type: 'loading' }
  | { type: 'error'; message: string }
  | { type: 'decisions'; data: DecisionItem[] };

// Webview → extension host
export type FromAgentsWebview =
  | { type: 'run'; agentSlug: string }
  | { type: 'stop'; agentSlug: string };

export type FromMessagesWebview =
  | { type: 'process'; fileName: string; agentSlug: string }; // agentSlug populated from MessageItem.agentSlug

export type FromDecisionsWebview =
  | { type: 'resolve'; decisionId: string; resolution: string };
