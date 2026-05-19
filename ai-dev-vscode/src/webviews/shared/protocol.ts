import type { AgentSummary, MessageItem, DecisionItem, BoardData, BoardTaskItem } from '../../types';
import type { LogEntry } from '../../Logger';

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

export type ToKanbanWebview =
  | { type: 'loading' }
  | { type: 'error'; message: string }
  | { type: 'board'; data: BoardData; githubRepo?: string }
  | { type: 'github-sign-in-required'; owner: string; repo: string };

// Webview → extension host
export type FromAgentsWebview =
  | { type: 'ready' }
  | { type: 'run'; agentSlug: string }
  | { type: 'stop'; agentSlug: string };

export type FromMessagesWebview =
  | { type: 'ready' }
  | { type: 'process'; fileName: string; agentSlug: string }; // agentSlug populated from MessageItem.agentSlug

export type FromDecisionsWebview =
  | { type: 'ready' }
  | { type: 'resolve'; decisionId: string; resolution: string };

export type FromKanbanWebview =
  | { type: 'ready' }
  | { type: 'refresh' }
  | { type: 'githubSignIn' }
  | {
      type: 'createTask';
      columnId: string;
      title: string;
      description?: string;
      assignee?: string;
      priority?: string;
      tags?: string[];
    }
  | {
      type: 'updateTask';
      taskId: string;
      columnId: string;
      title: string;
      description?: string;
      assignee?: string;
      priority?: string;
      tags?: string[];
    }
  | { type: 'moveTask'; taskId: string; toColumnId: string }
  | { type: 'deleteTask'; taskId: string }
  | { type: 'addColumn'; id: string; title: string }
  | { type: 'renameColumn'; columnId: string; title: string }
  | { type: 'deleteColumn'; columnId: string };

export type EditableBoardTask = Pick<
  BoardTaskItem,
  'id' | 'title' | 'description' | 'assignee' | 'priority' | 'tags'
>;

// Extension host → Logs webview
export type ToLogsWebview =
  | { type: 'history'; entries: LogEntry[] }
  | { type: 'entry'; entry: LogEntry }
  | { type: 'cleared' };

// Logs webview → extension host
export type FromLogsWebview =
  | { type: 'clear' };

export type { LogEntry };
