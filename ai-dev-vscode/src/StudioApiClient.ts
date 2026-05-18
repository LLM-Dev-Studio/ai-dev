export type { AgentSummary, MessageItem, DecisionItem, BoardData, BoardTaskItem } from './types';
import type { AgentSummary, MessageItem, DecisionItem, BoardData, BoardTaskItem } from './types';

export type Fetcher = (url: string, init?: RequestInit) => Promise<Response>;

export class StudioApiClient {
  constructor(
    private readonly baseUrl: string,
    private readonly fetcher: Fetcher = (url, init) => fetch(url, init),
  ) {}

  // ── Agents ──────────────────────────────────────────────────────────────────

  async listAgents(projectSlug: string): Promise<AgentSummary[]> {
    return this.get(`/api/agents?projectSlug=${enc(projectSlug)}`);
  }

  async runAgent(projectSlug: string, agentSlug: string): Promise<void> {
    await this.post(`/api/agents/${enc(agentSlug)}/run?projectSlug=${enc(projectSlug)}`);
  }

  async stopAgent(projectSlug: string, agentSlug: string): Promise<void> {
    await this.post(`/api/agents/${enc(agentSlug)}/stop?projectSlug=${enc(projectSlug)}`);
  }

  // ── Messages ─────────────────────────────────────────────────────────────────

  async listMessages(projectSlug: string, agentSlug?: string, processed?: boolean): Promise<MessageItem[]> {
    let url = `/api/messages?projectSlug=${enc(projectSlug)}`;
    if (agentSlug) url += `&agentSlug=${enc(agentSlug)}`;
    if (processed !== undefined) url += `&processed=${processed}`;
    return this.get(url);
  }

  async listAllMessages(projectSlug: string): Promise<MessageItem[]> {
    const agents = await this.listAgents(projectSlug);
    const perAgent = await Promise.all(
      agents.map(a =>
        this.listMessages(projectSlug, a.slug, false)
          .then(msgs => msgs.map(m => ({ ...m, agentSlug: a.slug }))),
      ),
    );
    return perAgent.flat();
  }

  async processMessage(projectSlug: string, agentSlug: string, fileName: string): Promise<void> {
    await this.post(
      `/api/messages/${enc(fileName)}/process?projectSlug=${enc(projectSlug)}&agentSlug=${enc(agentSlug)}`,
    );
  }

  // ── Decisions ────────────────────────────────────────────────────────────────

  async listDecisions(projectSlug: string, status?: string): Promise<DecisionItem[]> {
    let url = `/api/decisions?projectSlug=${enc(projectSlug)}`;
    if (status) url += `&status=${enc(status)}`;
    return this.get(url);
  }

  async resolveDecision(projectSlug: string, decisionId: string, resolution: string): Promise<void> {
    await this.post(`/api/decisions/${enc(decisionId)}/resolve?projectSlug=${enc(projectSlug)}`, {
      resolution,
    });
  }

  // ── Board ───────────────────────────────────────────────────────────────────

  async getBoard(projectSlug: string): Promise<BoardData> {
    return this.get(`/api/board?projectSlug=${enc(projectSlug)}`);
  }

  async createBoardTask(
    projectSlug: string,
    request: {
      columnId: string;
      title: string;
      description?: string;
      priority?: string;
      assignee?: string;
      tags?: string[];
    },
  ): Promise<BoardTaskItem> {
    return this.postJson(`/api/board/tasks?projectSlug=${enc(projectSlug)}`, request);
  }

  async updateBoardTask(
    projectSlug: string,
    taskId: string,
    request: {
      columnId: string;
      title: string;
      description?: string;
      priority?: string;
      assignee?: string;
      tags?: string[];
    },
  ): Promise<BoardTaskItem> {
    return this.postJson(`/api/board/tasks/${enc(taskId)}?projectSlug=${enc(projectSlug)}`, request);
  }

  async deleteBoardTask(projectSlug: string, taskId: string): Promise<void> {
    await this.del(`/api/board/tasks/${enc(taskId)}?projectSlug=${enc(projectSlug)}`);
  }

  // ── Helpers ──────────────────────────────────────────────────────────────────

  private async get<T>(path: string): Promise<T> {
    const res = await this.fetcher(this.baseUrl + path);
    if (!res.ok) throw new ApiError(res.status, path);
    return res.json() as Promise<T>;
  }

  private async post(path: string, body?: unknown): Promise<void> {
    const res = await this.fetcher(this.baseUrl + path, {
      method: 'POST',
      headers: body ? { 'Content-Type': 'application/json' } : undefined,
      body: body ? JSON.stringify(body) : undefined,
    });
    if (!res.ok) throw new ApiError(res.status, path);
  }

  private async postJson<T>(path: string, body: unknown): Promise<T> {
    const res = await this.fetcher(this.baseUrl + path, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    });
    if (!res.ok) throw new ApiError(res.status, path);
    return res.json() as Promise<T>;
  }

  private async del(path: string): Promise<void> {
    const res = await this.fetcher(this.baseUrl + path, {
      method: 'DELETE',
    });
    if (!res.ok) throw new ApiError(res.status, path);
  }
}

function enc(value: string): string {
  return encodeURIComponent(value);
}

export class ApiError extends Error {
  constructor(
    public readonly statusCode: number,
    public readonly path: string,
  ) {
    super(`API error ${statusCode} for ${path}`);
    this.name = 'ApiError';
  }
}
