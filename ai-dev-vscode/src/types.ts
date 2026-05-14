export interface ProjectConfig {
  projectSlug: string;
  apiPort: number;
}

export interface DetectedProject {
  config: ProjectConfig;
  workspaceFolderPath: string;
}

// Shared data shapes used by both extension host and webview apps

export interface AgentSummary {
  slug: string;
  isRunning: boolean;
  isRateLimited: boolean;
}

export interface MessageItem {
  fileName: string;
  from: string;
  re: string;
  type: string;
  priority: string;
  createdAt: string;
  processed: boolean;
  agentSlug?: string; // populated client-side when fetching per-agent
}

export interface DecisionItem {
  id: string;
  from: string;
  subject: string;
  priority: string;
  status: string;
  createdAt: string;
  blocks?: string;
  body?: string;
}
