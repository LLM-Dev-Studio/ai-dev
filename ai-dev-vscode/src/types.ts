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

export interface BoardColumnItem {
  id: string;
  title: string;
  taskIds: string[];
}

export interface BoardTaskItem {
  id: string;
  title: string;
  priority: string;
  description?: string;
  assignee?: string;
  tags?: string[];
  createdAt?: string;
  completedAt?: string;
  movedAt?: string;
  nudgedAt?: string;
}

export interface BoardData {
  columns: BoardColumnItem[];
  tasks: Record<string, BoardTaskItem>;
}

export interface GitHubRepoInfo {
  owner: string;
  repo: string;
}
